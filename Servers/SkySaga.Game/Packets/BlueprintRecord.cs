namespace SkySaga.Game.Packets;

public class BlueprintRecord
{
    private const uint PriceMax = 0x200;
    private const uint CountMax = 0x8000;

    private const uint SizeXMax = 0x400;
    private const uint SizeYMax = 0xE0;
    private const uint SizeZMax = 0x400;

    private const uint BoundsXMax = 0x800;
    private const uint BoundsYMax = 0x1C0;
    private const uint BoundsZMax = 0x800;

    private const uint PhotoCountMax = 5;
    private const uint TagCountMax = 3;

    public string? Name;
    public string? Description;
    public string? AuthorName;

    public Guid? BlueprintUuid;
    public Guid? AuthorUuid;

    public uint PriceSoft;
    public uint PriceHard;
    public uint SalesCount;
    public uint ViewCount;

    public Vector3Int Size;
    public Vector3Int Bounds;

    public bool IsPublic;

    public List<Guid?> PhotoUuids = [];
    public List<uint?> Tags = [];

    public uint[] Words = new uint[7];

    public bool Flag;

    public void Encode(BitStream bitStream)
    {
        bitStream.WriteString(Name);
        bitStream.WriteString(Description);
        bitStream.WriteString(AuthorName);

        InventoryItemRef.WriteOptionalUuid(bitStream, BlueprintUuid);
        InventoryItemRef.WriteOptionalUuid(bitStream, AuthorUuid);

        bitStream.WriteRanged(Math.Min(PriceSoft, PriceMax), PriceMax);
        bitStream.WriteRanged(Math.Min(PriceHard, PriceMax), PriceMax);
        bitStream.WriteRanged(Math.Min(SalesCount, CountMax), CountMax);
        bitStream.WriteRanged(Math.Min(ViewCount, CountMax), CountMax);

        WriteClamped(bitStream, Size.X, SizeXMax);
        WriteClamped(bitStream, Size.Y, SizeYMax);
        WriteClamped(bitStream, Size.Z, SizeZMax);

        WriteClamped(bitStream, Bounds.X, BoundsXMax);
        WriteClamped(bitStream, Bounds.Y, BoundsYMax);
        WriteClamped(bitStream, Bounds.Z, BoundsZMax);

        bitStream.Write(IsPublic);

        WriteCount(bitStream, (uint)PhotoUuids.Count, PhotoCountMax);
        foreach (var uuid in PhotoUuids)
            InventoryItemRef.WriteOptionalUuid(bitStream, uuid);

        WriteCount(bitStream, (uint)Tags.Count, TagCountMax);
        foreach (var tag in Tags)
            InventoryItemRef.WriteOptionalUInt32(bitStream, tag);

        for (var i = 0; i < 7; i++)
            InventoryItemRef.WriteUInt32(bitStream, i < Words.Length ? Words[i] : 0);

        bitStream.Write(Flag);
    }

    private static void WriteClamped(BitStream bitStream, int value, uint max)
    {
        bitStream.WriteRanged((uint)Math.Clamp(value, 0, (int)max), max);
    }

    private static void WriteCount(BitStream bitStream, uint count, uint max)
    {
        bitStream.WriteRanged(Math.Min(count, max), max);

        if (count < max)
            return;

        bitStream.Write1();
        InventoryItemRef.WriteUInt32(bitStream, count);
    }
}
