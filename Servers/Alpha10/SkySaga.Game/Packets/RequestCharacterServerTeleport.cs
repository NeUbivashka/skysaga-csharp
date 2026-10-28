using SkySaga.Game.Database;

namespace SkySaga.Game.Packets;

public static class RequestCharacterServerTeleport
{
    private const uint ReasonMax = 0x11;

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadClientUuid(out var target)) return false;
        if (!bitStream.ReadInt32(1, out var flag)) return false;
        if (!bitStream.ReadFullWidthUInt32(out var value)) return false;
        if (!bitStream.ReadInt32(ReasonMax, out var reason)) return false;

        Console.WriteLine($"[Travel] {connection.CharacterName} asks to join {target} (flag {flag}, value {value}, reason {reason})");

        if (!connection.IsPlayerInitialized || connection.CharacterId is not { } characterId || target is not { } targetId)
            return true;

        return WorldTravel.Request(connection, characterId, nameof(RequestCharacterServerTeleport),
            _ => WorldStore.OnlineWorld(targetId) is { } worldId ? connection.Server.Worlds.Find(worldId) : null);
    }
}
