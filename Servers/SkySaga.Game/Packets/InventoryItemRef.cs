namespace SkySaga.Game.Packets;

/// <summary>
/// The compound value FUN_007E9440 reads. Shared by KillOccurred (36), DismantleFailed (57) and
/// NewResourceEncountered (58), which is why it lives in its own file rather than being spelled
/// out three times.
///
/// Field order and widths, straight from the reader:
///
///     Kind       FUN_007D8110   ranged index into the item table ([0x01487BCC]+0x1F28, count
///                               at +0x1F2C = 1810 in a live client) -> 11 bits. It was written as
///                               16 bits, 5 too many.
///     Amounts    FUN_007E6140   count: ranged max 4 -> 3 bits, escape at 4 (1 bit + 32 bits);
///                               then per entry: 1 presence bit, and 32 bits if set
///     Quantity   FUN_007EA4B0   1 presence bit + 32 bits
///     ItemUuid   FUN_007EA3B0   1 presence bit + 4 x 32 bits
///     Detail     FUN_007EA060   a variant: ranged max 2 -> 2 bits of kind, then for kind 0 an
///                               optional UUID, for kind 1 a string (FUN_007EA530), else nothing.
///                               It was written as kind + string + UUID.
///
/// Names are by role. Nothing here names itself: the loggers for all three packets run through
/// packed thunks and lose their string pointers. The shape is solid, the labels are a reading.
/// </summary>
public class InventoryItemRef
{
    private const uint AmountCountMax = 4;

    /// <summary>Item table size in this build (live client, [0x01487BCC]+0x1F2C).</summary>
    public const uint ItemTableCount = 1810;
    private const uint DetailKindMax = 2;

    public ushort Kind;

    public List<uint?> Amounts = [];

    public uint? Quantity;

    public Guid? ItemUuid;

    public uint DetailKind;

    public string? DetailName;

    public Guid? DetailUuid;

    public void Encode(BitStream bitStream)
    {
        bitStream.WriteRangedIndex(Kind, ItemTableCount);

        var count = (uint)Amounts.Count;

        bitStream.WriteRanged(Math.Min(count, AmountCountMax), AmountCountMax);

        if (count >= AmountCountMax)
        {
            bitStream.Write1();
            WriteUInt32(bitStream, count);
        }

        foreach (var amount in Amounts)
            WriteOptionalUInt32(bitStream, amount);

        WriteOptionalUInt32(bitStream, Quantity);
        WriteOptionalUuid(bitStream, ItemUuid);

        var detailKind = Math.Min(DetailKind, DetailKindMax);

        bitStream.WriteRanged(detailKind, DetailKindMax);

        if (detailKind == 0)
            WriteOptionalUuid(bitStream, DetailUuid);
        else if (detailKind == 1)
            bitStream.WriteString(DetailName);
    }

    /// <summary>Reads what Encode writes (the client's writer is FUN_0085A490).</summary>
    public static bool TryDecode(BitStream bitStream, out InventoryItemRef result)
    {
        result = new InventoryItemRef();

        if (!bitStream.ReadInt32(ItemTableCount - 1, out var kind)) return false;
        result.Kind = (ushort)kind;

        if (!bitStream.ReadEscapedCount(AmountCountMax, out var count)) return false;

        for (var i = 0; i < count; i++)
        {
            if (!ReadOptionalUInt32(bitStream, out var amount)) return false;
            result.Amounts.Add(amount);
        }

        if (!ReadOptionalUInt32(bitStream, out result.Quantity)) return false;
        if (!bitStream.ReadClientUuid(out result.ItemUuid)) return false;
        if (!bitStream.ReadInt32(DetailKindMax, out var detailKind)) return false;
        result.DetailKind = (uint)detailKind;

        if (detailKind == 0)
            return bitStream.ReadClientUuid(out result.DetailUuid);

        if (detailKind == 1)
            result.DetailName = bitStream.ReadString();

        return true;
    }

    private static bool ReadOptionalUInt32(BitStream bitStream, out uint? value)
    {
        value = null;

        if (!bitStream.ReadInt32(1, out var present)) return false;
        if (present == 0) return true;
        if (!bitStream.ReadFullWidthUInt32(out var v)) return false;

        value = v;
        return true;
    }

    internal static void WriteUInt32(BitStream bitStream, uint value)
    {
        var bytes = BitConverter.GetBytes(value);

        Array.Reverse(bytes);

        bitStream.WriteBits(bytes, 32, true);
    }

    internal static void WriteOptionalUInt32(BitStream bitStream, uint? value)
    {
        if (value is null)
        {
            bitStream.Write0();

            return;
        }

        bitStream.Write1();

        WriteUInt32(bitStream, value.Value);
    }

    internal static void WriteOptionalUuid(BitStream bitStream, Guid? value)
    {
        bitStream.WriteClientUuid(value);
    }
}
