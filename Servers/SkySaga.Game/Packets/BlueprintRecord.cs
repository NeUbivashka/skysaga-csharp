namespace SkySaga.Game.Packets;

/// <summary>
/// The blueprint record FUN_007E8430 reads. Shared by BlueprintShopPurchaseFailed (125),
/// BlueprintRevokeResult (127) and BlueprintSaleClaimResult (131).
///
///     Name           string
///     Description    string
///     AuthorName     string
///     BlueprintUuid  1 presence bit + 4 x 32 bits
///     AuthorUuid     1 presence bit + 4 x 32 bits
///     PriceSoft      FUN_007D7400: ranged, max 0x200 -> 10 bits
///     PriceHard      FUN_007D7400: ranged, max 0x200 -> 10 bits
///     SalesCount     FUN_007D7740: ranged, max 0x8000 -> 16 bits
///     ViewCount      FUN_007D7740: ranged, max 0x8000 -> 16 bits
///     Size           FUN_007D6C10: three ranged, max 0x400, 0xE0, 0x400 -> 11, 8, 11 bits
///     Bounds         FUN_007D68B0: three ranged, max 0x800, 0x1C0, 0x800 -> 12, 9, 12 bits
///     IsPublic       1 bit
///     PhotoUuids     FUN_007E5CE0: count ranged max 5 (escape at 5), then that many optional UUIDs
///     Tags           FUN_007E5FB0: count ranged max 3 (escape at 3), then that many optional u32
///     SevenWords     seven 32-bit fields, full width -> big-endian
///     Flag           1 bit
///
/// This record was deferred twice for looking bottomless, and the reason it looked that way is
/// that most of its call graph does not read. FUN_0077A2B0, FUN_00795160, FUN_00795DF0,
/// FUN_0079BD40, FUN_00806C20 and FUN_00AC2620 are all container plumbing - reserving, copying,
/// clamping - and once they are set aside what remains is a flat list of ordinary fields.
///
/// Names are by role throughout. Nothing in these three packets survives with its logger names
/// intact, so the labels are a reading of shape and context; the widths and the order are not.
/// </summary>
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

    /// <summary>Seven trailing 32-bit fields, purpose unknown.</summary>
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

    /// <summary>The usual shape: the count, then an escape bit and a full width if it hits the max.</summary>
    private static void WriteCount(BitStream bitStream, uint count, uint max)
    {
        bitStream.WriteRanged(Math.Min(count, max), max);

        if (count < max)
            return;

        bitStream.Write1();
        InventoryItemRef.WriteUInt32(bitStream, count);
    }
}
