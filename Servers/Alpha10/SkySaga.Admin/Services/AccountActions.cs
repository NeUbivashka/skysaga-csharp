using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SkySaga.Data;
using SkySaga.Data.Entities;

namespace SkySaga.Admin.Services;

public sealed class AccountActions(IDbContextFactory<SkySagaDbContext> dbFactory)
{
    public async Task SetPremiumAsync(Guid accountId, DateTimeOffset? until)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Accounts.Where(a => a.Id == accountId).ExecuteUpdateAsync(set => set.SetProperty(a => a.PremiumUntil, until));
        await RecordAsync(db, "admin.premium", accountId, new { until = Forever.Is(until) ? "forever" : until?.ToString("O") });
    }

    public async Task SetBanAsync(Guid accountId, DateTimeOffset? until, string? reason)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Accounts.Where(a => a.Id == accountId)
            .ExecuteUpdateAsync(set => set.SetProperty(a => a.BannedUntil, until).SetProperty(a => a.BanReason, until is null ? null : reason));

        if (until is not null)
            await AccountSecurity.SignOutEverywhereAsync(db, accountId);

        await RecordAsync(db, until is null ? "admin.unban" : "admin.ban", accountId,
            until is null ? null : new { until = Forever.Is(until) ? "forever" : until.Value.ToString("O"), reason });
    }

    public async Task SetRoleAsync(Guid accountId, AccountRole role)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Accounts.Where(a => a.Id == accountId).ExecuteUpdateAsync(set => set.SetProperty(a => a.Role, role));
        await RecordAsync(db, "admin.role", accountId, new { role = role.ToString() });
    }

    public async Task SignOutEverywhereAsync(Guid accountId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await AccountSecurity.SignOutEverywhereAsync(db, accountId);
        await RecordAsync(db, "admin.sign_out", accountId);
    }

    public async Task UnlockAsync(Guid accountId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Accounts.Where(a => a.Id == accountId)
            .ExecuteUpdateAsync(set => set.SetProperty(a => a.LockedUntil, (DateTimeOffset?)null).SetProperty(a => a.FailedLogins, 0));
        await RecordAsync(db, "admin.unlock", accountId);
    }

    public async Task<string> CreateResetCodeAsync(Guid accountId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var code = await AccountSecurity.CreateResetCodeAsync(db, accountId);
        await RecordAsync(db, "admin.password_reset", accountId);

        return code;
    }

    public async Task SetWorldAccessAsync(Guid worldId, WorldAccess visit, WorldAccess build)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Worlds.Where(w => w.Id == worldId)
            .ExecuteUpdateAsync(set => set.SetProperty(w => w.VisitAccess, visit).SetProperty(w => w.BuildAccess, build));

        db.GameEvents.Add(new GameEvent
        {
            Time = DateTimeOffset.UtcNow,
            Kind = "admin.world_access",
            WorldId = worldId,
            Data = JsonSerializer.Serialize(new { visit = visit.ToString(), build = build.ToString() })
        });
        await db.SaveChangesAsync();
    }

    private static async Task RecordAsync(SkySagaDbContext db, string kind, Guid accountId, object? details = null)
    {
        var account = await db.Accounts.AsNoTracking().Where(a => a.Id == accountId)
            .Select(a => new { a.Username, CharacterId = a.Characters.OrderBy(c => c.CreatedAt).Select(c => (Guid?)c.Id).FirstOrDefault() })
            .SingleAsync();

        db.GameEvents.Add(new GameEvent
        {
            Time = DateTimeOffset.UtcNow,
            Kind = kind,
            CharacterId = account.CharacterId,
            Data = JsonSerializer.Serialize(new { account = account.Username, details })
        });
        await db.SaveChangesAsync();
    }
}
