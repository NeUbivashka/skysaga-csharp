namespace SkySaga.Game.Managers.World;

using System.IO.Compression;

public static class WorldMapLoader
{
    public const string DefaultPath = @"Data\WorldMap.json";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };





    public static bool TryLoad(IMapChunkManager chunkManager, out MapDefinition? definition, out List<WorldMapEntityData> entities, string path = DefaultPath)
    {
        ArgumentNullException.ThrowIfNull(chunkManager);

        definition = null;
        entities = [];

        string actualPath = path;
        bool isGzip = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);

        if (!File.Exists(actualPath))
        {

            string gzPath = path + ".gz";
            if (!isGzip && File.Exists(gzPath))
            {
                actualPath = gzPath;
                isGzip = true;
            }
            else
            {
                return false;
            }
        }

        WorldMapData? data;

        try
        {
            using var fileStream = File.OpenRead(actualPath);
            using Stream sourceStream = isGzip
                ? new GZipStream(fileStream, CompressionMode.Decompress)
                : fileStream;
            data = JsonSerializer.Deserialize<WorldMapData>(sourceStream, _jsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidDataException)
        {
            Console.WriteLine($"[WorldMapLoader] Failed to read '{actualPath}': {ex.Message}");
            return false;
        }

        if (data is null || data.Voxels is null || data.Voxels.Count == 0)
        {
            Console.WriteLine($"[WorldMapLoader] '{actualPath}' has no voxels, ignoring.");
            return false;
        }

        int sizeX = Math.Clamp(data.MapSizeChunks.X, 1, 32);
        int sizeY = Math.Clamp(data.MapSizeChunks.Y, 1, 32);
        int sizeZ = Math.Clamp(data.MapSizeChunks.Z, 1, 32);

        // SKYSAGA_MAP_SIZE="X,Y,Z" overrides the chunk bounds for testing (FTUE is 10,5,10), without
        // editing WorldMap.json. Voxels stay where the file put them.
        if (Environment.GetEnvironmentVariable("SKYSAGA_MAP_SIZE") is { } sizeOverride
            && sizeOverride.Split(',') is [var ox, var oy, var oz]
            && int.TryParse(ox, out var sx) && int.TryParse(oy, out var sy) && int.TryParse(oz, out var sz))
        {
            sizeX = Math.Clamp(sx, 1, 32);
            sizeY = Math.Clamp(sy, 1, 7);  // ChunkSync carries Y ranged max 7
            sizeZ = Math.Clamp(sz, 1, 32);
            Console.WriteLine($"[WorldMapLoader] SKYSAGA_MAP_SIZE - chunk bounds {sizeX}x{sizeY}x{sizeZ}.");
        }

        int placed = 0;
        int skippedOutOfBounds = 0;

        foreach (var voxel in data.Voxels)
        {
            var chunkPos = new Vector3Int(
                FloorDiv(voxel.X, 32),
                FloorDiv(voxel.Y, 32),
                FloorDiv(voxel.Z, 32));


            if (chunkPos.X < 0 || chunkPos.X >= sizeX ||
                chunkPos.Y < 0 || chunkPos.Y >= sizeY ||
                chunkPos.Z < 0 || chunkPos.Z >= sizeZ)
            {
                skippedOutOfBounds++;
                continue;
            }

            int localX = Mod(voxel.X, 32);
            int localY = Mod(voxel.Y, 32);
            int localZ = Mod(voxel.Z, 32);

            var chunk = chunkManager.GetOrCreateChunk(chunkPos);
            chunk.SetVoxel(localX, localY, localZ, voxel.Block, voxel.Meta);
            placed++;
        }

        // Every chunk inside the map bounds must exist, air-only ones included. The client computes
        // sunlight itself ("Light Propagation task calculating sunlight", FUN_0097DFF0) over the
        // whole map; with only the 16 ground chunks synced, the layers above were never sent.
        // Hypothesis under test: that is what leaves the ground unlit (the black world).
        for (int cx = 0; cx < sizeX; cx++)
            for (int cy = 0; cy < sizeY; cy++)
                for (int cz = 0; cz < sizeZ; cz++)
                    chunkManager.GetOrCreateChunk(new Vector3Int(cx, cy, cz));

        // SKYSAGA_SOLID_FLOOR=1: fill every chunk of the bottom layer completely with block 1. A full
        // 32-voxel slab is a floor whatever axis order the client uses for chunk data, so it tells an
        // axis-order mismatch apart from other causes (the player falling through the 2-layer floor).
        if (Environment.GetEnvironmentVariable("SKYSAGA_SOLID_FLOOR") == "1")
        {
            for (int cx = 0; cx < sizeX; cx++)
                for (int cz = 0; cz < sizeZ; cz++)
                {
                    var chunk = chunkManager.GetOrCreateChunk(new Vector3Int(cx, 0, cz));
                    for (int x = 0; x < 32; x++)
                        for (int y = 0; y < 32; y++)
                            for (int z = 0; z < 32; z++)
                                chunk.SetVoxel(x, y, z, 1, 0);
                }

            Console.WriteLine("[WorldMapLoader] SKYSAGA_SOLID_FLOOR - bottom chunk layer filled solid.");
        }

        // MapSpec.Ftue hard-coded slot 11 (MapSizes) = 7 = MapSize_10x5x10, while this map is
        // sizeX x sizeY x sizeZ. The client then expected a 10x5x10 world and lit it from a top
        // the server never sent. Describe the map we actually load.
        var spec = MapSpec.FromEnvironment(seed: 1337);

        if (!spec.IsFtue && (sizeX, sizeY, sizeZ) == (6, 4, 6))
            spec.Searchable[MapSpec.MapSizeSlot] = MapSpec.HomeIslandMapSizeIndex;
        else if (MapSpec.TryGetMapSizeWireIndex(sizeX, sizeY, sizeZ, out var mapSizeIndex))
            spec.Searchable[MapSpec.MapSizeSlot] = mapSizeIndex;
        else
            Console.WriteLine($"[WorldMapLoader] No GeoData MapSize for {sizeX}x{sizeY}x{sizeZ} chunks - "
                + $"the client will assume slot {spec.Searchable[MapSpec.MapSizeSlot]}. Use a listed size.");

        definition = new MapDefinition
        {
            MapSizeChunks = new Vector3Int(sizeX, sizeY, sizeZ),







            Spec = spec,
            GameMode = (uint)data.GameMode,
            AdventureType = "Adventure",

            MapFileName = "TestMap"
        };

        if (data.Entities is not null)
            entities = data.Entities;

        Console.WriteLine(
            $"[WorldMapLoader] Loaded '{actualPath}'{(isGzip ? " (gzip)" : "")}: {placed} voxels placed, {entities.Count} entities" +
            (skippedOutOfBounds > 0 ? $", {skippedOutOfBounds} voxels skipped (out of {sizeX}x{sizeY}x{sizeZ} chunk bounds)" : ""));

        return true;
    }

    private static int FloorDiv(int value, int divisor) =>
        (int)Math.Floor(value / (double)divisor);

    private static int Mod(int value, int modulus) =>
        ((value % modulus) + modulus) % modulus;





    private static MapSpec BuildSpecForBiome(string? biomeName)
    {
        var spec = MapSpec.HomeIsland(seed: 1337);

        var biomeIndex = biomeName?.Trim().ToLowerInvariant() switch
        {
            "desert" => 1u,
            "forest" => 2u,
            "jungle" => 3u,
            "mountain" => 4u,
            "npcfriend" => 5u,
            _ => 1u
        };

        spec.Searchable[0] = biomeIndex;

        return spec;
    }
}
