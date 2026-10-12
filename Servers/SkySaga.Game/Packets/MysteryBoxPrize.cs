namespace SkySaga.Game.Packets;

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
