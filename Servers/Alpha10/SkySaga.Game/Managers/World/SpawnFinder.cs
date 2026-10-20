namespace SkySaga.Game.Managers.World;

public static class SpawnFinder
{
    private const int ChunkSize = 32;

    public static bool IsSolidAt(IMapChunkManager chunkManager, Vector3 position)
    {
        ArgumentNullException.ThrowIfNull(chunkManager);

        int x = (int)MathF.Floor(position.X), y = (int)MathF.Floor(position.Y), z = (int)MathF.Floor(position.Z);

        var chunkPosition = new Vector3Int(FloorDiv(x, ChunkSize), FloorDiv(y, ChunkSize), FloorDiv(z, ChunkSize));
        var voxelPosition = new Vector3Int(Mod(x, ChunkSize), Mod(y, ChunkSize), Mod(z, ChunkSize));

        return chunkManager.GetVoxel(chunkPosition, voxelPosition) != WorldChunk.Air;
    }

    public static float? GroundBelow(IMapChunkManager chunkManager, Vector3 position, float rise)
    {
        ArgumentNullException.ThrowIfNull(chunkManager);

        int x = (int)MathF.Floor(position.X), z = (int)MathF.Floor(position.Z);

        for (var y = (int)MathF.Floor(position.Y + rise); y >= 0; y--)
        {
            var chunkPosition = new Vector3Int(FloorDiv(x, ChunkSize), FloorDiv(y, ChunkSize), FloorDiv(z, ChunkSize));
            var voxelPosition = new Vector3Int(Mod(x, ChunkSize), Mod(y, ChunkSize), Mod(z, ChunkSize));

            if (chunkManager.GetVoxel(chunkPosition, voxelPosition) != WorldChunk.Air)
                return y + 1;
        }

        return null;
    }

    public static int FindGroundHeight(IMapChunkManager chunkManager, int worldX, int worldZ, int mapHeightVoxels)
    {
        ArgumentNullException.ThrowIfNull(chunkManager);

        for (int y = mapHeightVoxels - 1; y >= 0; y--)
        {
            var chunkPosition = new Vector3Int(FloorDiv(worldX, ChunkSize), FloorDiv(y, ChunkSize), FloorDiv(worldZ, ChunkSize));
            var voxelPosition = new Vector3Int(Mod(worldX, ChunkSize), Mod(y, ChunkSize), Mod(worldZ, ChunkSize));

            if (chunkManager.GetVoxel(chunkPosition, voxelPosition) != WorldChunk.Air)
                return y;
        }

        return -1;
    }

    public static Vector3 FindSafeSpawnPoint(IMapChunkManager chunkManager, Vector3Int mapSizeChunks)
    {
        int sizeXVoxels = Math.Max(mapSizeChunks.X, 1) * ChunkSize;
        int sizeYVoxels = Math.Max(mapSizeChunks.Y, 1) * ChunkSize;
        int sizeZVoxels = Math.Max(mapSizeChunks.Z, 1) * ChunkSize;

        int centerX = sizeXVoxels / 2;
        int centerZ = sizeZVoxels / 2;

        var found = FindGroundNearby(chunkManager, centerX, centerZ, sizeXVoxels, sizeZVoxels, sizeYVoxels);

        if (found is { } point)
            return point;

        return new Vector3(centerX + 0.5f, sizeYVoxels * 0.6f, centerZ + 0.5f);
    }

    public static Vector3 SnapToGroundOrDefault(IMapChunkManager chunkManager, float worldX, float worldZ, int mapHeightVoxels, Vector3 fallback)
    {
        int groundY = FindGroundHeight(chunkManager, (int)MathF.Floor(worldX), (int)MathF.Floor(worldZ), mapHeightVoxels);

        if (groundY < 0)
            return fallback;

        return new Vector3(worldX, groundY + 1.1f, worldZ);
    }

    public static Vector3? FindEdgeSpawnPoint(IMapChunkManager chunkManager, Vector3Int mapSizeChunks, Random random)
    {
        ArgumentNullException.ThrowIfNull(chunkManager);
        ArgumentNullException.ThrowIfNull(random);

        const int step = 4;
        const int edgeCheckRadius = 8;

        int sizeX = Math.Max(mapSizeChunks.X, 1) * ChunkSize;
        int sizeY = Math.Max(mapSizeChunks.Y, 1) * ChunkSize;
        int sizeZ = Math.Max(mapSizeChunks.Z, 1) * ChunkSize;

        int cols = (sizeX / step) + 1;
        int rows = (sizeZ / step) + 1;
        var heights = new int[cols, rows];

        for (int cx = 0; cx < cols; cx++)
        {
            int x = Math.Min(cx * step, sizeX - 1);

            for (int cz = 0; cz < rows; cz++)
            {
                int z = Math.Min(cz * step, sizeZ - 1);
                heights[cx, cz] = FindGroundHeight(chunkManager, x, z, sizeY);
            }
        }

        int neighborSteps = Math.Max(edgeCheckRadius / step, 1);
        List<(int cx, int cz)> candidates = [];

        for (int cx = 0; cx < cols; cx++)
        {
            for (int cz = 0; cz < rows; cz++)
            {
                int groundY = heights[cx, cz];

                if (groundY < 0)
                    continue;

                if (IsNearEdge(heights, cx, cz, groundY, cols, rows, neighborSteps))
                    candidates.Add((cx, cz));
            }
        }

        if (candidates.Count == 0)
            return null;

        var (pickCx, pickCz) = candidates[random.Next(candidates.Count)];
        int px = Math.Min(pickCx * step, sizeX - 1);
        int pz = Math.Min(pickCz * step, sizeZ - 1);
        int py = heights[pickCx, pickCz];

        return new Vector3(px + 0.5f, py + 1.1f, pz + 0.5f);
    }

    private static bool IsNearEdge(int[,] heights, int cx, int cz, int groundY, int cols, int rows, int neighborSteps)
    {
        for (int dcx = -neighborSteps; dcx <= neighborSteps; dcx++)
        {
            for (int dcz = -neighborSteps; dcz <= neighborSteps; dcz++)
            {
                int ncx = cx + dcx;
                int ncz = cz + dcz;

                if (ncx < 0 || ncx >= cols || ncz < 0 || ncz >= rows)
                    return true;

                int neighborGround = heights[ncx, ncz];

                if (neighborGround < 0 || neighborGround < groundY - 4)
                    return true;
            }
        }

        return false;
    }

    public static Vector3 FindSpawnNearPoint(IMapChunkManager chunkManager, Vector3 center, int minRadius, int maxRadius, Vector3Int mapSizeChunks, Random random)
    {
        ArgumentNullException.ThrowIfNull(chunkManager);
        ArgumentNullException.ThrowIfNull(random);

        int sizeX = Math.Max(mapSizeChunks.X, 1) * ChunkSize;
        int sizeY = Math.Max(mapSizeChunks.Y, 1) * ChunkSize;
        int sizeZ = Math.Max(mapSizeChunks.Z, 1) * ChunkSize;

        int centerX = (int)MathF.Floor(center.X);
        int centerZ = (int)MathF.Floor(center.Z);

        List<Vector3> candidates = [];
        int minRadiusSq = minRadius * minRadius;
        int maxRadiusSq = maxRadius * maxRadius;

        for (int dx = -maxRadius; dx <= maxRadius; dx++)
        {
            for (int dz = -maxRadius; dz <= maxRadius; dz++)
            {
                int distSq = dx * dx + dz * dz;

                if (distSq < minRadiusSq || distSq > maxRadiusSq)
                    continue;

                int x = centerX + dx;
                int z = centerZ + dz;

                if (x < 0 || x >= sizeX || z < 0 || z >= sizeZ)
                    continue;

                int groundY = FindGroundHeight(chunkManager, x, z, sizeY);

                if (groundY >= 0)
                    candidates.Add(new Vector3(x + 0.5f, groundY + 1.1f, z + 0.5f));
            }
        }

        if (candidates.Count > 0)
            return candidates[random.Next(candidates.Count)];

        var widened = FindGroundNearby(chunkManager, centerX, centerZ, sizeX, sizeZ, sizeY);

        if (widened is { } point)
            return point;

        return center;
    }

    public static Vector3? FindStandingPoint(IMapChunkManager chunkManager, Vector3 point, Vector3Int mapSizeChunks, int maxRadius, int minRadius = 0)
    {
        ArgumentNullException.ThrowIfNull(chunkManager);

        int sizeX = Math.Max(mapSizeChunks.X, 1) * ChunkSize;
        int sizeY = Math.Max(mapSizeChunks.Y, 1) * ChunkSize;
        int sizeZ = Math.Max(mapSizeChunks.Z, 1) * ChunkSize;

        int centerX = (int)MathF.Floor(point.X);
        int centerZ = (int)MathF.Floor(point.Z);
        int startY = Math.Min((int)MathF.Floor(point.Y) + StandingSearchHeadroom, sizeY - 1);

        for (int radius = minRadius; radius <= maxRadius; radius++)
        {
            Vector3? best = null;
            float bestDistance = float.MaxValue;

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != radius)
                        continue;

                    int x = centerX + dx;
                    int z = centerZ + dz;

                    if (x < 0 || x >= sizeX || z < 0 || z >= sizeZ)
                        continue;

                    int groundY = FindStandableGround(chunkManager, x, z, startY);

                    if (groundY < 0)
                        continue;

                    var candidate = radius == 0
                        ? new Vector3(point.X, groundY + 1.1f, point.Z)
                        : new Vector3(x + 0.5f, groundY + 1.1f, z + 0.5f);

                    var distance = Vector3.DistanceSquared(candidate, point);

                    if (distance < bestDistance)
                    {
                        best = candidate;
                        bestDistance = distance;
                    }
                }
            }

            if (best is not null)
                return best;
        }

        return null;
    }

    private const int StandingSearchHeadroom = 4;

    private static int FindStandableGround(IMapChunkManager chunkManager, int x, int z, int startY)
    {
        for (int y = startY; y >= 0; y--)
        {
            if (IsSolidAt(chunkManager, new Vector3(x, y, z))
                && !IsSolidAt(chunkManager, new Vector3(x, y + 1, z))
                && !IsSolidAt(chunkManager, new Vector3(x, y + 2, z)))
                return y;
        }

        return -1;
    }

    private static Vector3? FindGroundNearby(IMapChunkManager chunkManager, int centerX, int centerZ, int sizeX, int sizeZ, int sizeY)
    {
        const int maxRadius = 48;
        const int radiusStep = 4;
        const int pointsPerRing = 16;

        var centerGround = FindGroundHeight(chunkManager, centerX, centerZ, sizeY);

        if (centerGround >= 0)
            return new Vector3(centerX + 0.5f, centerGround + 1.1f, centerZ + 0.5f);

        for (int radius = radiusStep; radius <= maxRadius; radius += radiusStep)
        {
            for (int angleStep = 0; angleStep < pointsPerRing; angleStep++)
            {
                double angle = angleStep * (Math.PI * 2 / pointsPerRing);
                int x = centerX + (int)(Math.Cos(angle) * radius);
                int z = centerZ + (int)(Math.Sin(angle) * radius);

                if (x < 0 || x >= sizeX || z < 0 || z >= sizeZ)
                    continue;

                int groundY = FindGroundHeight(chunkManager, x, z, sizeY);

                if (groundY >= 0)
                    return new Vector3(x + 0.5f, groundY + 1.1f, z + 0.5f);
            }
        }

        return null;
    }

    private static int FloorDiv(int value, int divisor) =>
        (int)Math.Floor(value / (double)divisor);

    private static int Mod(int value, int modulus) =>
        ((value % modulus) + modulus) % modulus;
}
