namespace SkySaga.Game.Managers.World;

public interface IWorldManager
{
    WorldInfo Info { get; }
    System.Collections.Concurrent.ConcurrentDictionary<Vector3Int, float> VoxelDamage { get; }
    System.Collections.Concurrent.ConcurrentDictionary<Vector3Int, DateTime> VoxelRegeneration { get; }
    void HitVoxel(Vector3Int voxel, DateTime now);
    void RegenerateVoxels(DateTime now);
    IMapEntityManager EntityManager { get; }
    IMapChunkManager ChunkManager { get; }
    MapDefinition Definition { get; set; }

    (Vector3 Position, float YawDegrees) GetPlayerLandingPoint();
    (Vector3 Position, float YawDegrees) GetRespawnPoint(Guid? character);
    void SetCheckpoint(Guid character, Entity checkpoint);

    void SendInitialChunks(PlayerConnection connection);
    void SendInitialEntities(PlayerConnection connection, Entity player, PlayerInitializer playerInitializer);
    IEnumerable<Entity> InventoryFirst(IEnumerable<Entity> entities);
    List<Entity> SpawnCreature(string name, Vector3 position);
    Entity? SpawnTree(string typeName, Vector3Int root, int seed, bool fromMap);
    bool IsDugEntity(int entityId);
    string? CutTreeBlock(Vector3Int block);
    void DeviceInventoryChanged(Entity entity);
    bool IsOreVein(int entityId);
    Entity? TakeOreVein(Vector3Int block);
    List<int> RemoveDevice(Entity entity, bool placedOnly, out WorldManager.PlacedDevice? placed);
    List<Entity> PlaceDevice(uint itemCrc, DeviceInfo device, Vector3Int voxel, Vector3 surfaceNormal, Guid? owner, uint?[] materials);
    List<Entity> RespawnMapCreature(Entity creature);
    (Vector3 Position, string? SpawnParams)? MapSpawnOf(int entityId);
    void Reset();
}
