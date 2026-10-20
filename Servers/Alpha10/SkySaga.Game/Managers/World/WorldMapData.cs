namespace SkySaga.Game.Managers.World;

public class WorldMapData
{
    public WorldMapSizeData MapSizeChunks { get; set; } = new();
    public string? BiomeName { get; set; }
    public int GameMode { get; set; } = 1;
    public List<WorldMapVoxelData> Voxels { get; set; } = [];
    public List<WorldMapEntityData>? Entities { get; set; }
}

public class WorldMapSizeData
{
    public int X { get; set; } = 4;
    public int Y { get; set; } = 4;
    public int Z { get; set; } = 4;
}

public class WorldMapVoxelData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public byte Block { get; set; }
    public byte Meta { get; set; }
}

public class WorldMapEntityData
{
    public string Name { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public List<string>? Items { get; set; }
    public string? MentalState { get; set; }
    public float? Yaw { get; set; }
}
