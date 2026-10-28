using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace SkySaga.Game.Database;

public static class SocialStore
{
    public static async Task<IReadOnlyCollection<string>> BlockersOfAsync(string characterName)
    {
        await using var context = GameDatabase.Context();

        return await context.CharacterBlocks.AsNoTracking()
            .Where(b => context.Characters.Any(c => c.Id == b.BlockedId && c.Name == characterName))
            .Join(context.Characters, b => b.BlockerId, c => c.Id, (b, c) => c.Name)
            .ToListAsync();
    }
}
