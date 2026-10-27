using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SkySaga.Data;
using SkySaga.Data.Entities;

namespace SkySaga.Auth.Services;

public enum LoginOutcome
{
    Success,
    Invalid,
    Locked,
    Banned
}

public sealed record IssuedToken(string TokenId, string RefreshingTokenId, int TimeoutSeconds);

public sealed partial class AccountService(IDbContextFactory<SkySagaDbContext> factory, EmailProtector emails, ILogger<AccountService> log)
{
    public const int TokenTimeoutSeconds = 86400;
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;

    private const int LockoutThreshold = 5;
    private static readonly TimeSpan LockoutTime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LauncherLifetime = TimeSpan.FromHours(12);
    private static readonly TimeSpan GameLifetime = TimeSpan.FromSeconds(TokenTimeoutSeconds * 2);
    private static readonly string DummyHash = PasswordHasher.Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));

    [GeneratedRegex("^[A-Za-z0-9_.-]{3,32}$")]
    private static partial Regex UsernamePattern();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]{2,15}$")]
    private static partial Regex CharacterNamePattern();

    public static string? UsernameProblem(string? username)
        => username is not null && UsernamePattern().IsMatch(username)
            ? null
            : "Use 3 to 32 letters, digits, dots, dashes or underscores.";

    public static string? CharacterNameProblem(string? name)
        => name is not null && CharacterNamePattern().IsMatch(name)
            ? null
            : "Use 3 to 16 letters, digits or underscores, starting with a letter.";

    public static string? PasswordProblem(string? password, string? username)
    {
        if (password is null || password.Length < MinPasswordLength)
            return $"Use at least {MinPasswordLength} characters.";

        if (password.Length > MaxPasswordLength)
            return $"Use at most {MaxPasswordLength} characters.";

        if (username is not null && string.Equals(password, username, StringComparison.OrdinalIgnoreCase))
            return "The password must not be your username.";

        return null;
    }

    public async Task<(Account? Account, string? Field, string? Error)> RegisterAsync(string email, string username, string password)
    {
        if (EmailProtector.Problem(email) is { } emailProblem)
            return (null, "email", emailProblem);

        if (UsernameProblem(username) is { } usernameProblem)
            return (null, "username", usernameProblem);

        if (PasswordProblem(password, username) is { } passwordProblem)
            return (null, "password", passwordProblem);

        await using var db = await factory.CreateDbContextAsync();

        var emailIndex = emails.Index(email);

        if (await db.Accounts.AnyAsync(a => a.EmailIndex == emailIndex))
            return (null, "email", "An account with this e-mail already exists.");

        if (await FindAccountAsync(db, username) is not null)
            return (null, "username", "This username is taken.");

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = PasswordHasher.Hash(password),
            EmailIndex = emailIndex,
            EmailProtected = emails.Protect(email),
            EmailHint = EmailProtector.Mask(email),
            SecurityStamp = Guid.NewGuid(),
            Role = AccountRole.Player,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        log.LogInformation("Registered account {Username}", username);

        return (account, null, null);
    }

    public async Task<(LoginOutcome Outcome, Account? Account)> LoginAsync(string? login, string? password)
    {
        await using var db = await factory.CreateDbContextAsync();
        var account = await FindByLoginAsync(db, login);
        var now = DateTimeOffset.UtcNow;

        if (account?.PasswordHash is null || string.IsNullOrEmpty(password))
        {
            PasswordHasher.Verify(password ?? "", DummyHash);
            log.LogInformation("Login refused for {Login}: unknown account or no password",
                login is not null && EmailProtector.LooksLikeEmail(login) ? EmailProtector.Mask(login) : login);
            return (LoginOutcome.Invalid, null);
        }

        if (account.LockedUntil > now)
        {
            log.LogInformation("Login refused for {Username}: locked until {Until}", account.Username, account.LockedUntil);
            return (LoginOutcome.Locked, null);
        }

        if (!PasswordHasher.Verify(password, account.PasswordHash))
        {
            account.FailedLogins++;

            if (account.FailedLogins >= LockoutThreshold)
            {
                account.FailedLogins = 0;
                account.LockedUntil = now + LockoutTime;
            }

            await db.SaveChangesAsync();
            log.LogInformation("Login refused for {Username}: wrong password", account.Username);
            return (LoginOutcome.Invalid, null);
        }

        account.FailedLogins = 0;
        account.LockedUntil = null;

        if (account.BannedUntil > now)
        {
            await db.SaveChangesAsync();
            log.LogInformation("Login refused for {Username}: banned until {Until}", account.Username, account.BannedUntil);
            return (LoginOutcome.Banned, account);
        }

        account.LastLoginAt = now;

        if (account.SecurityStamp == Guid.Empty)
            account.SecurityStamp = Guid.NewGuid();

        await db.SaveChangesAsync();
        log.LogInformation("Login accepted for {Username}", account.Username);

        return (LoginOutcome.Success, account);
    }

    public async Task<string?> ChangePasswordAsync(Guid accountId, string current, string next)
    {
        await using var db = await factory.CreateDbContextAsync();
        var account = await db.Accounts.SingleAsync(a => a.Id == accountId);

        if (!PasswordHasher.Verify(current, account.PasswordHash))
            return "The current password is wrong.";

        if (PasswordProblem(next, account.Username) is { } problem)
            return problem;

        await SetPasswordAsync(db, account, next);
        log.LogInformation("Password changed for {Username}", account.Username);

        return null;
    }

    public async Task<(string? Field, string? Error)> SetEmailAsync(Guid accountId, string email, string password)
    {
        if (EmailProtector.Problem(email) is { } problem)
            return ("email", problem);

        await using var db = await factory.CreateDbContextAsync();
        var account = await db.Accounts.SingleAsync(a => a.Id == accountId);

        if (!PasswordHasher.Verify(password, account.PasswordHash))
            return ("password", "The password is wrong.");

        var index = emails.Index(email);

        if (await db.Accounts.AnyAsync(a => a.EmailIndex == index && a.Id != accountId))
            return ("email", "Another account uses this e-mail.");

        account.EmailIndex = index;
        account.EmailProtected = emails.Protect(email);
        account.EmailHint = EmailProtector.Mask(email);
        await db.SaveChangesAsync();

        log.LogInformation("E-mail changed for {Username}", account.Username);

        return (null, null);
    }

    public async Task SignOutEverywhereAsync(Guid accountId)
    {
        await using var db = await factory.CreateDbContextAsync();
        await AccountSecurity.SignOutEverywhereAsync(db, accountId);

        log.LogInformation("Signed account {AccountId} out everywhere", accountId);
    }

    public async Task<string> CreateResetCodeAsync(Guid accountId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await AccountSecurity.CreateResetCodeAsync(db, accountId);
    }

    public async Task<(string? Field, string? Error)> ResetPasswordAsync(string code, string password)
    {
        await using var db = await factory.CreateDbContextAsync();
        var hash = SessionTokens.Hash(code.Trim().ToUpperInvariant());
        var now = DateTimeOffset.UtcNow;
        var reset = await db.PasswordResets.Include(r => r.Account)
            .SingleOrDefaultAsync(r => r.CodeHash == hash && r.UsedAt == null && r.ExpiresAt > now);

        if (reset?.Account is not { } account)
            return ("code", "This code is wrong, used or expired. Ask an admin for a new one.");

        if (PasswordProblem(password, account.Username) is { } problem)
            return ("password", problem);

        reset.UsedAt = now;
        await SetPasswordAsync(db, account, password);
        log.LogInformation("Password reset for {Username}", account.Username);

        return (null, null);
    }

    public async Task<IssuedToken> IssueAsync(Guid accountId, SessionKind kind)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await IssueAsync(db, accountId, kind);
    }

    public async Task<AuthSession?> FindSessionAsync(string? token, SessionKind kind)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        await using var db = await factory.CreateDbContextAsync();
        var hash = SessionTokens.Hash(token);
        var now = DateTimeOffset.UtcNow;
        var session = await db.AuthSessions.Include(s => s.Account)
            .SingleOrDefaultAsync(s => s.TokenHash == hash && s.Kind == kind && s.ExpiresAt > now);

        if (session is null || session.Account!.BannedUntil > now)
            return null;

        session.LastUsedAt = now;
        await db.SaveChangesAsync();

        return session;
    }

    public async Task<IssuedToken?> ConvertLauncherTokenAsync(string? launcherToken)
    {
        if (string.IsNullOrWhiteSpace(launcherToken))
            return null;

        await using var db = await factory.CreateDbContextAsync();
        var hash = SessionTokens.Hash(launcherToken);
        var now = DateTimeOffset.UtcNow;
        var session = await db.AuthSessions.Include(s => s.Account)
            .SingleOrDefaultAsync(s => s.TokenHash == hash && s.Kind == SessionKind.Launcher && s.ExpiresAt > now);

        if (session is null || session.Account!.BannedUntil > now)
            return null;

        db.AuthSessions.Remove(session);
        var issued = await IssueAsync(db, session.AccountId, SessionKind.Game);

        log.LogInformation("Game session started for {Username}", session.Account.Username);

        return issued;
    }

    public async Task<IssuedToken?> RefreshAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        await using var db = await factory.CreateDbContextAsync();
        var hash = SessionTokens.Hash(token);
        var now = DateTimeOffset.UtcNow;
        var session = await db.AuthSessions.Include(s => s.Account)
            .SingleOrDefaultAsync(s => (s.RefreshHash == hash || s.TokenHash == hash) && s.Kind == SessionKind.Game && s.ExpiresAt > now);

        if (session is null || session.Account!.BannedUntil > now)
            return null;

        db.AuthSessions.Remove(session);

        return await IssueAsync(db, session.AccountId, SessionKind.Game);
    }

    public async Task RevokeAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        await using var db = await factory.CreateDbContextAsync();
        var hash = SessionTokens.Hash(token);

        await db.AuthSessions.Where(s => s.TokenHash == hash).ExecuteDeleteAsync();
    }

    public async Task<Account?> GetAccountAsync(Guid accountId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == accountId);
    }

    public async Task<Character?> GetCharacterAsync(Guid accountId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Characters.AsNoTracking().Where(c => c.AccountId == accountId).OrderBy(c => c.CreatedAt).FirstOrDefaultAsync();
    }

    public async Task<Character> CreateCharacterAsync(Guid accountId)
    {
        await using var db = await factory.CreateDbContextAsync();

        if (await db.Characters.Where(c => c.AccountId == accountId).OrderBy(c => c.CreatedAt).FirstOrDefaultAsync() is { } existing)
            return existing;

        var account = await db.Accounts.SingleAsync(a => a.Id == accountId);
        var name = account.Username;

        while (await NameTakenAsync(db, name, accountId))
            name = $"{account.Username}_{RandomNumberGenerator.GetInt32(10000):D4}";

        var character = new Character { Id = Guid.NewGuid(), AccountId = accountId, Name = name, CreatedAt = DateTimeOffset.UtcNow };

        db.Characters.Add(character);
        await db.SaveChangesAsync();

        log.LogInformation("Created character {Name} for {Username}", name, account.Username);

        return character;
    }

    public async Task<bool> IsNameTakenAsync(string name, Guid? accountId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await NameTakenAsync(db, name, accountId);
    }

    public async Task<Account?> FindAccountAsync(string username)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await FindAccountAsync(db, username);
    }

    private async Task<Account?> FindByLoginAsync(SkySagaDbContext db, string? login)
    {
        if (string.IsNullOrWhiteSpace(login))
            return null;

        if (!EmailProtector.LooksLikeEmail(login))
            return await FindAccountAsync(db, login.Trim(), track: true);

        var index = emails.Index(login);

        return await db.Accounts.SingleOrDefaultAsync(a => a.EmailIndex == index);
    }

    private static Task<Account?> FindAccountAsync(SkySagaDbContext db, string username, bool track = false)
    {
        var lowered = username.ToLowerInvariant();
        var accounts = track ? db.Accounts : db.Accounts.AsNoTracking();

        return accounts.SingleOrDefaultAsync(a => a.Username.ToLower() == lowered);
    }

    private static async Task<bool> NameTakenAsync(SkySagaDbContext db, string name, Guid? accountId)
    {
        var lowered = name.ToLowerInvariant();

        return await db.Characters.AnyAsync(c => c.Name.ToLower() == lowered && c.AccountId != accountId);
    }

    private static async Task SetPasswordAsync(SkySagaDbContext db, Account account, string password)
    {
        account.PasswordHash = PasswordHasher.Hash(password);
        account.SecurityStamp = Guid.NewGuid();
        account.FailedLogins = 0;
        account.LockedUntil = null;

        await db.AuthSessions.Where(s => s.AccountId == account.Id).ExecuteDeleteAsync();
        await db.SaveChangesAsync();
    }

    private static async Task<IssuedToken> IssueAsync(SkySagaDbContext db, Guid accountId, SessionKind kind)
    {
        var token = SessionTokens.New();
        var refresh = SessionTokens.New();
        var now = DateTimeOffset.UtcNow;

        db.AuthSessions.Add(new AuthSession
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Kind = kind,
            TokenHash = SessionTokens.Hash(token),
            RefreshHash = SessionTokens.Hash(refresh),
            CreatedAt = now,
            ExpiresAt = now + (kind == SessionKind.Launcher ? LauncherLifetime : GameLifetime)
        });
        await db.AuthSessions.Where(s => s.AccountId == accountId && s.ExpiresAt <= now).ExecuteDeleteAsync();
        await db.SaveChangesAsync();

        return new IssuedToken(token, refresh, TokenTimeoutSeconds);
    }
}
