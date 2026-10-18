namespace SkySaga.Game.Managers.World;

public interface IWorldManager
{
    IMapEntityManager EntityManager { get; }
    IMapChunkManager ChunkManager { get; }
    MapDefinition Definition { get; set; }

    (Vector3 Position, float YawDegrees) GetPlayerLandingPoint();

    void SendInitialChunks(PlayerConnection connection);
    void SendInitialEntities(PlayerConnection connection, Entity player, PlayerInitializer playerInitializer);
    List<Entity> SpawnCreature(string name, Vector3 position);
    List<Entity> RespawnMapCreature(Entity creature);
    void Reset();
}
