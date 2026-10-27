using SkySaga.Game.Database;

namespace SkySaga.Game.Packets;

public static class JoinSocialHub
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadFullWidthUInt32(out var triggeringEntityId)) return false;

        return WorldTravel.Request(connection, triggeringEntityId, nameof(JoinSocialHub), _ => WorldStore.Hub());
    }
}
