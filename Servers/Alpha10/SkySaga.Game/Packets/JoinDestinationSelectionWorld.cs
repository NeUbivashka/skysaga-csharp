using SkySaga.Game.Database;

namespace SkySaga.Game.Packets;

public static class JoinDestinationSelectionWorld
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadFullWidthUInt32(out var triggeringEntityId)) return false;

        return WorldTravel.Request(connection, triggeringEntityId, nameof(JoinDestinationSelectionWorld), _ => WorldStore.Hub());
    }
}
