using SkySaga.Data.Entities;
using SkySaga.Game.Database;

namespace SkySaga.Game.Packets;

public static class RequestAdventureStart
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!MapSpec.TryDecode(bitStream, out var spec)) return false;
        if (!bitStream.ReadFullWidthUInt32(out var entityId)) return false;
        if (!bitStream.ReadInt32(1, out var flag)) return false;

        Console.WriteLine($"[Travel] {connection.CharacterName} asks to start an adventure {spec} (entity {entityId}, flag {flag})");

        if (!connection.IsPlayerInitialized || connection.CharacterId is not { } characterId)
            return true;

        return WorldTravel.Request(connection, characterId, nameof(RequestAdventureStart),
            owner => WorldStore.Instance(WorldKind.Adventure, owner, spec));
    }
}
