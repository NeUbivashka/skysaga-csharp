namespace SkySaga.Game.Packets;

/// <summary>
/// The compound value FUN_007E92D0 reads. Used by MysteryBoxOpenedNotification (168) directly and
/// by MysteryBoxPrizesDetermined (166) as its list element.
///
///     Item        FUN_00C04130: one InventoryItemRef followed by 32 bits
///     Slot        8 bits, read raw with no swap
///     Quantities  FUN_007E62D0: count ranged max 8 (escape at 8), then per entry a presence bit
///                 and 32 bits if set - the same list shape InventoryItemRef.Amounts uses
/// </summary>
public class MysteryBoxPrize
{
    private const uint QuantityCountMax = 8;

    public InventoryItemRef Item = new();

    public uint ItemCount;

    public byte Slot;

    public List<uint?> Quantities = [];

    public void Encode(BitStream bitStream)
    {
        Item.Encode(bitStream);

        InventoryItemRef.WriteUInt32(bitStream, ItemCount);

        bitStream.WriteBits([Slot], 8, true);

        var count = (uint)Quantities.Count;

        bitStream.WriteRanged(Math.Min(count, QuantityCountMax), QuantityCountMax);

        if (count >= QuantityCountMax)
        {
            bitStream.Write1();
            InventoryItemRef.WriteUInt32(bitStream, count);
        }

        foreach (var quantity in Quantities)
            InventoryItemRef.WriteOptionalUInt32(bitStream, quantity);
    }
}
