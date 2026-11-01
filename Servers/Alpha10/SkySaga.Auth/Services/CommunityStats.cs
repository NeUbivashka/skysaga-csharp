using Microsoft.EntityFrameworkCore;
using SkySaga.Data;
using SkySaga.Data.Entities;

namespace SkySaga.Auth.Services;

public sealed record Community(int Adventurers, int Isles, int Online);

public sealed class CommunityStats(IDbContextFactory<SkySagaDbContext> factory)
{
    public async Task<Community> GetAsync()
    {
        await using var db = await factory.CreateDbContextAsync();

        return new Community(
            await db.Accounts.CountAsync(),
            await db.Worlds.CountAsync(w => w.Kind == WorldKind.Island),
            await db.Characters.CountAsync(c => c.OnlineWorldId != null));
    }
}
