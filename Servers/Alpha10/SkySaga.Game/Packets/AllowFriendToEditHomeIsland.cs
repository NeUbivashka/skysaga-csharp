using System.Linq;
using SkySaga.Data.Entities;

namespace SkySaga.Game.Packets;

public static class AllowFriendToEditHomeIsland
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadClientUuid(out var friend) || !bitStream.ReadInt32(1, out var allow))
            return false;

        if (connection.CharacterId is not { } owner || friend is not { } friendId || friendId == owner)
        {
            Console.WriteLine($"[Build] {connection.CharacterName} permission for {friend} ignored");
            return true;
        }

        Console.WriteLine($"[Build] {connection.CharacterName} {(allow == 1 ? "allows" : "forbids")} {friendId} to build on their islands");

        foreach (var world in connection.Server.Worlds.Loaded.Where(w => w.Info.Kind == WorldKind.Island && w.Info.OwnerCharacterId == owner))
        {
            var guest = world.Connections.GetAllConnections().FirstOrDefault(c => c.IsPlayerInitialized && c.CharacterId == friendId);

            if (guest is null || !guest.PlayerEntity.TryGetComponent<ClientPlayerAspectsComponent>(out var aspects))
                continue;

            var canBuild = allow == 1 || world.Info.CanBuild(friendId);

            aspects.CanEditMap = canBuild;
            aspects.CanCreateDevices = canBuild;
            aspects.CanDamageDevices = canBuild;

            Console.WriteLine($"[Build] {guest.CharacterName} can build on {world.Info.Id}: {canBuild}");
        }

        return true;
    }
}
