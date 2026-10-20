namespace SkySaga.Game.Managers.World;

public interface IWorldManager
{
    IMapEntityManager EntityManager { get; }
    IMapChunkManager ChunkManager { get; }
    MapDefinition Definition { get; set; }

    (Vector3 Position, float YawDegrees) GetPlayerLandingPoint();
    (Vector3 Position, float YawDegrees) GetRespawnPoint(Guid? character);
    void SetCheckpoint(Guid character, Entity checkpoint);

    void SendInitialChunks(PlayerConnection connection);
    void SendInitialEntities(PlayerConnection connection, Entity player, PlayerInitializer playerInitializer);
    List<Entity> SpawnCreature(string name, Vector3 position);
    void DeviceInventoryChanged(Entity entity);
    List<int> RemoveDevice(Entity entity, bool placedOnly, out WorldManager.PlacedDevice? placed);
    List<Entity> PlaceDevice(uint itemCrc, DeviceInfo device, Vector3Int voxel, Guid? owner, uint?[] materials);
    List<Entity> RespawnMapCreature(Entity creature);
    (Vector3 Position, string? SpawnParams)? MapSpawnOf(int entityId);
    void Reset();
}
