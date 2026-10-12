namespace SkySaga.Game.Managers.World;

public interface IWorldManager
{
    IMapEntityManager EntityManager { get; }
    IMapChunkManager ChunkManager { get; }
    MapDefinition Definition { get; set; }

    Vector3 AirshipPosition { get; }

    Vector3? PlayerSpawnPosition { get; }

    void SendInitialChunks(PlayerConnection connection);
    void SendInitialEntities(PlayerConnection connection, Entity player, PlayerInitializer playerInitializer);
    void Reset();
}
