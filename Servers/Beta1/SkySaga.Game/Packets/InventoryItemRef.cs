namespace SkySaga.Game.Packets;

public class InventoryItemRef
{
    private const uint AmountCountMax = 4;

    public static uint ItemTableCount => ItemTable.Count;
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
