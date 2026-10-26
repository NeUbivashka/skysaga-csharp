using SkySaga.Data.Entities;

namespace SkySaga.Game.Database;

public static class GameEvents
{
    public static void Record(string kind, Guid? characterId = null, object? data = null)
    {
        var gameEvent = new GameEvent
        {
            Time = DateTimeOffset.UtcNow,
            Kind = kind,
            CharacterId = characterId,
            WorldId = WorldStore.WorldId(),
            Data = data is null ? null : JsonSerializer.Serialize(data)
        };

        GameDatabase.Enqueue(context =>
        {
            context.GameEvents.Add(gameEvent);
            return context.SaveChangesAsync();
        });
    }
}
