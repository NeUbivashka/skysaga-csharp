namespace SkySaga.Game.Packets;

public static class RequestWorldTeleport
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadClientUuid(out var worldId)) return false;
        if (!bitStream.ReadFullWidthUInt32(out var triggeringEntityId)) return false;

        return WorldTravel.Request(connection, triggeringEntityId, nameof(RequestWorldTeleport),
            _ => worldId is { } id ? connection.Server.Worlds.Find(id) : null);
    }
}
