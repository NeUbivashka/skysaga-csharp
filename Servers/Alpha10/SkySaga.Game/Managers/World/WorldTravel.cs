using SkySaga.Game.Database;

namespace SkySaga.Game.Managers.World;

public static class WorldTravel
{
    public static bool Request(PlayerConnection connection, uint triggeringEntityId, string request, Func<Guid, WorldInfo?> destinationFor)
    {
        if (!connection.IsPlayerInitialized || connection.CharacterId is not { } characterId
            || triggeringEntityId != (uint)connection.PlayerEntity.Id)
        {
            Console.WriteLine($"[Travel] {request} from entity {triggeringEntityId} ignored: the connection has no player with that entity.");
            return true;
        }

        var current = connection.WorldManager.Info;
        var destination = destinationFor(characterId);

        if (destination is null || !destination.CanVisit(characterId))
        {
            Console.WriteLine($"[Travel] {request}: {connection.CharacterName} may not go there from {current.Kind} {current.Id}.");
            return true;
        }

        if (destination.Id == current.Id)
            Console.WriteLine($"[Travel] {request}: {connection.CharacterName} is already in {current.Kind} {current.Id}; reloading it.");

        Send(connection, characterId, destination);
        return true;
    }

    private static void Send(PlayerConnection connection, Guid characterId, WorldInfo destination)
    {
        var world = connection.Server.Worlds.Open(destination);

        connection.SaveState();
        WorldStore.SetCurrentWorld(characterId, destination.Id);

        connection.Send(new TransferToServer
        {
            WorldUuid = destination.Id,
            ServerUuid = destination.Id,
            Host = connection.GameHost,
            Port = connection.Server.Port
        });

        connection.Send(new TeleportBegins { Spec = world.Manager.Definition.Spec });

        Console.WriteLine($"[Travel] {connection.CharacterName} leaves {connection.WorldManager.Info.Kind} {connection.WorldManager.Info.Id} "
            + $"for {destination.Kind} {destination.Id} at {connection.GameHost}:{connection.Server.Port}.");
    }
}
