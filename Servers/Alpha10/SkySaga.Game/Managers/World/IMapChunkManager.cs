namespace SkySaga.Game.Managers.World;

public interface IMapChunkManager
{
    bool TryGetChunk(Vector3Int chunkPosition, [NotNullWhen(true)] out WorldChunk? chunk);

    WorldChunk GetOrCreateChunk(Vector3Int chunkPosition);

    void SetChunk(Vector3Int chunkPosition, WorldChunk chunk);

    void RemoveChunk(Vector3Int chunkPosition);

    byte GetVoxel(Vector3Int chunkPosition, Vector3Int voxelPosition);

    void SetVoxel(Vector3Int chunkPosition, Vector3Int voxelPosition, byte blockType, byte metaValue = 0x00);

    void SetVoxelUnsaved(Vector3Int worldPosition, byte blockType);

    byte GetVoxel(Vector3Int worldPosition);

    void LoadSavedEdits();

    IEnumerable<WorldChunk> GetLoadedChunks();

    void GenerateInitialChunks();

    void ClearAll();

    bool HasEdit(Vector3Int worldPosition);

    ChunkSync GetChunkSyncData(Vector3Int chunkPosition);

    IEnumerable<ChunkSync> GetAllChunkSyncData();
}
