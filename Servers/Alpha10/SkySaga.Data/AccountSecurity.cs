using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SkySaga.Data.Entities;

namespace SkySaga.Data;

public static class AccountSecurity
{
    public static readonly TimeSpan ResetLifetime = TimeSpan.FromHours(24);

    public static async Task<string> CreateResetCodeAsync(SkySagaDbContext db, Guid accountId)
    {
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(10));
        var now = DateTimeOffset.UtcNow;

        await db.PasswordResets.Where(r => r.AccountId == accountId && r.UsedAt == null).ExecuteDeleteAsync();
        db.PasswordResets.Add(new PasswordReset
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            CodeHash = SessionTokens.Hash(code),
            CreatedAt = now,
            ExpiresAt = now + ResetLifetime
        });
        await db.SaveChangesAsync();

        return code;
    }

    public static async Task SignOutEverywhereAsync(SkySagaDbContext db, Guid accountId)
    {
        await db.AuthSessions.Where(s => s.AccountId == accountId).ExecuteDeleteAsync();
        await db.Accounts.Where(a => a.Id == accountId).ExecuteUpdateAsync(set => set.SetProperty(a => a.SecurityStamp, Guid.NewGuid()));
    }
}
