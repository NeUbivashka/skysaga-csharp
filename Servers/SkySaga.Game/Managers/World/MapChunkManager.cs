namespace SkySaga.Game.Managers.World;

public class MapChunkManager(PlayerConnectionManager playerConnectionManager) : IMapChunkManager
{
    private readonly Dictionary<Vector3Int, WorldChunk> _chunks = [];
    private readonly PlayerConnectionManager _playerConnectionManager = playerConnectionManager ?? throw new ArgumentNullException(nameof(playerConnectionManager));

    public bool TryGetChunk(Vector3Int chunkPosition, [NotNullWhen(true)] out WorldChunk? chunk)
    {
        return _chunks.TryGetValue(chunkPosition, out chunk);
    }

    public WorldChunk GetOrCreateChunk(Vector3Int chunkPosition)
    {
        if (!_chunks.TryGetValue(chunkPosition, out var chunk))
        {
            chunk = new WorldChunk(chunkPosition);
            _chunks[chunkPosition] = chunk;
        }
        return chunk;
    }

    public void SetChunk(Vector3Int chunkPosition, WorldChunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        _chunks[chunkPosition] = chunk;
    }

    public void RemoveChunk(Vector3Int chunkPosition)
    {
        _chunks.Remove(chunkPosition);
    }

    public byte GetVoxel(Vector3Int chunkPosition, Vector3Int voxelPosition)
    {
        if (!TryGetChunk(chunkPosition, out var chunk))
            return 0;

        return chunk.GetVoxel(voxelPosition);
    }

    public void SetVoxel(Vector3Int chunkPosition, Vector3Int voxelPosition, byte blockType, byte metaValue = 0x00)
    {
        var chunk = GetOrCreateChunk(chunkPosition);

        byte oldBlockType = chunk.GetVoxel(voxelPosition);

        chunk.SetVoxel(voxelPosition, blockType, metaValue);

        var partialSync = GetPartialChunkEditSync(chunkPosition, voxelPosition, oldBlockType, blockType);
        _playerConnectionManager.BroadcastToAll(partialSync);
    }

    public static PartialChunkEditsSync GetPartialChunkEditSync(Vector3Int chunkPosition, Vector3Int voxelPosition, byte oldBlockType, byte newBlockType)
    {
        if (newBlockType == 0)
        {
            newBlockType = 255;
        }

        return new PartialChunkEditsSync
        {
            ChunkCoord = chunkPosition,
            ChunkEditList =
            [
                new ChunkEdit
                {
                    VoxelIndex = newBlockType,
                    VoxelCoordList =
                    [
                        voxelPosition
                    ]
                }
            ]
        };
    }

    public IEnumerable<WorldChunk> GetLoadedChunks() => _chunks.Values;

    public void GenerateInitialChunks()
    {
    }

    public void ClearAll()
    {
        _chunks.Clear();
    }

    public ChunkSync GetChunkSyncData(Vector3Int chunkPosition)
    {
        if (!TryGetChunk(chunkPosition, out var chunk))
            throw new InvalidOperationException($"Chunk at {chunkPosition} not loaded");

        var voxelData = chunk.GetVoxelData();

        const int chunkSize = 32;
        const int layerSize = chunkSize * chunkSize;
        const int layersToSync = chunkSize;

        int dataSize = layerSize * layersToSync;

        var syncData = new byte[dataSize + 1];
        var syncMeta = new byte[dataSize + 1];

        for (int i = 0; i < dataSize; i++)
        {
            syncData[1 + i] = (voxelData[i] == 0) ? (byte)255 : voxelData[i];
        }

        Array.Fill<byte>(syncMeta, 0x00);

        if (Environment.GetEnvironmentVariable("SKYSAGA_DATA2_UNIFORM") is { Length: > 0 } uniformHex
            && byte.TryParse(uniformHex, System.Globalization.NumberStyles.HexNumber, null, out var uniformValue))
        {
            syncMeta = [0x03, uniformValue];
        }

        return new ChunkSync
        {
            Coords = new Vector3Int(chunkPosition.X, chunkPosition.Y, chunkPosition.Z),
            Data1 = syncData,

            Data2 = syncMeta,
        };
    }

    public IEnumerable<ChunkSync> GetAllChunkSyncData()
    {
        foreach (var chunk in _chunks.Values)
        {
            yield return GetChunkSyncData(chunk.Position);
        }
    }
}
