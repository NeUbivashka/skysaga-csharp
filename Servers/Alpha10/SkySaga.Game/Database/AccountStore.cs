using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace SkySaga.Game.Database;

public static class AccountStore
{
    public static DateTimeOffset? PremiumUntil(Guid characterId)
    {
        using var context = GameDatabase.Context();
        return context.Characters.AsNoTracking().Where(c => c.Id == characterId).Select(c => c.Account!.PremiumUntil).SingleOrDefault();
    }
}
