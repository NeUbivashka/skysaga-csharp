using System.Linq;

namespace SkySaga.Game.Managers.World;

public class MapChunkManager(PlayerConnectionManager playerConnectionManager) : IMapChunkManager
{
    private const int ChunkSize = 32;
    private static readonly string MapSuffix = Environment.GetEnvironmentVariable("SKYSAGA_MAP") is { Length: > 0 } map
        ? $"_{Path.GetFileNameWithoutExtension(map)}"
        : "";
    private static readonly string EditsPath = Path.Combine(AppContext.BaseDirectory, "Data", "Saves", $"voxel_edits{MapSuffix}.json");
    private static readonly string LegacyEditsPath = Path.Combine(AppContext.BaseDirectory, "Data", "Saves", $"world_edits{MapSuffix}.json");
    private const byte LegacyAir = 0;

    private readonly Dictionary<Vector3Int, WorldChunk> _chunks = [];
    private readonly Dictionary<Vector3Int, byte> _edits = [];
    private readonly Lock _editsLock = new();
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
            return WorldChunk.Air;

        return chunk.GetVoxel(voxelPosition);
    }

    public void SetVoxel(Vector3Int chunkPosition, Vector3Int voxelPosition, byte blockType, byte metaValue = 0x00)
    {
        var chunk = GetOrCreateChunk(chunkPosition);

        byte oldBlockType = chunk.GetVoxel(voxelPosition);

        chunk.SetVoxel(voxelPosition, blockType, metaValue);
        RecordEdit(chunkPosition, voxelPosition, blockType);

        var partialSync = GetPartialChunkEditSync(chunkPosition, voxelPosition, oldBlockType, blockType);
        _playerConnectionManager.BroadcastToAll(partialSync);
    }

    public void LoadSavedEdits()
    {
        var legacy = !File.Exists(EditsPath);
        var path = legacy ? LegacyEditsPath : EditsPath;

        if (!File.Exists(path))
            return;

        int[][]? entries;

        try
        {
            entries = JsonSerializer.Deserialize<int[][]>(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            Console.WriteLine($"[World] Failed to read '{path}': {ex.Message}");
            return;
        }

        if (entries is null)
            return;

        if (legacy)
            foreach (var entry in entries)
                if (entry.Length == 4 && entry[3] == LegacyAir)
                    entry[3] = WorldChunk.Air;

        lock (_editsLock)
        {
            foreach (var entry in entries)
            {
                if (entry.Length != 4 || entry[0] < 0 || entry[1] < 0 || entry[2] < 0)
                    continue;

                var world = new Vector3Int(entry[0], entry[1], entry[2]);
                var chunkPosition = new Vector3Int(world.X / ChunkSize, world.Y / ChunkSize, world.Z / ChunkSize);

                if (!_chunks.TryGetValue(chunkPosition, out var chunk))
                    continue;

                chunk.SetVoxel(new Vector3Int(world.X % ChunkSize, world.Y % ChunkSize, world.Z % ChunkSize), (byte)entry[3]);
                _edits[world] = (byte)entry[3];
            }
        }

        Console.WriteLine($"[World] Applied {_edits.Count} saved voxel edits");
    }

    private void RecordEdit(Vector3Int chunkPosition, Vector3Int voxelPosition, byte blockType)
    {
        var world = new Vector3Int(
            chunkPosition.X * ChunkSize + voxelPosition.X,
            chunkPosition.Y * ChunkSize + voxelPosition.Y,
            chunkPosition.Z * ChunkSize + voxelPosition.Z);

        lock (_editsLock)
        {
            _edits[world] = blockType;

            var entries = _edits.Select(e => new[] { e.Key.X, e.Key.Y, e.Key.Z, e.Value }).ToArray();

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(EditsPath)!);
                File.WriteAllText(EditsPath, JsonSerializer.Serialize(entries));
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[World] Failed to save voxel edits: {ex.Message}");
            }
        }
    }

    public static PartialChunkEditsSync GetPartialChunkEditSync(Vector3Int chunkPosition, Vector3Int voxelPosition, byte oldBlockType, byte newBlockType)
    {
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
            syncData[1 + i] = voxelData[i];
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
