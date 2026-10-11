namespace SkySaga.Game.Packets;

/// <summary>
/// The compound value FUN_007E9440 reads. Shared by KillOccurred (36), DismantleFailed (57) and
/// NewResourceEncountered (58), which is why it lives in its own file rather than being spelled
/// out three times.
///
/// Field order and widths, straight from the reader:
///
///     Kind       FUN_007D8110   16 bits
///     Amounts    FUN_007E6140   count: ranged max 4 -> 3 bits, escape at 4 (1 bit + 32 bits);
///                               then per entry: 1 presence bit, and 32 bits if set
///     Quantity   FUN_007EA4B0   1 presence bit + 32 bits
///     ItemUuid   FUN_007EA3B0   1 presence bit + 4 x 32 bits
///     Detail     FUN_007EA060   ranged max 2 -> 2 bits, then a string, then another optional UUID
///
/// Names are by role. Nothing here names itself: the loggers for all three packets run through
/// packed thunks and lose their string pointers. The shape is solid, the labels are a reading.
/// </summary>
public class InventoryItemRef
{
    private const uint AmountCountMax = 4;
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
        bitStream.WriteBits(BitConverter.GetBytes(Kind), 16, true);

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

        bitStream.WriteRanged(Math.Min(DetailKind, DetailKindMax), DetailKindMax);
        bitStream.WriteString(DetailName);
        WriteOptionalUuid(bitStream, DetailUuid);
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
