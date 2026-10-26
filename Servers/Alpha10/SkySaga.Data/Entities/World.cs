namespace SkySaga.Data.Entities;

public class World
{
    public Guid Id { get; set; }
    public required string MapName { get; set; }
    public Guid? OwnerCharacterId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class VoxelEdit
{
    public Guid WorldId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public short Block { get; set; }
}

public class PlacedDevice
{
    public long Id { get; set; }
    public Guid WorldId { get; set; }
    public long ItemCrc { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public Guid? OwnerCharacterId { get; set; }
    public long?[] Materials { get; set; } = [];
    public string Items { get; set; } = "[]";
}

public class Checkpoint
{
    public Guid WorldId { get; set; }
    public Guid CharacterId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
}

public class GameEvent
{
    public long Id { get; set; }
    public DateTimeOffset Time { get; set; }
    public required string Kind { get; set; }
    public Guid? CharacterId { get; set; }
    public Guid? WorldId { get; set; }
    public string? Data { get; set; }
}
