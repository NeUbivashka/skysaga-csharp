namespace SkySaga.Game.Packets;

public static class InventoryItemDestroy
{
    private const byte SlotMax = 0xF9;
    private const uint SmallQuantityMax = 0x40;

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!Read(bitStream, out var ownerId, out var slot, out var quantity))
            return false;

        if (InventoryItems.Take(connection, ownerId, slot, quantity) is not { } taken)
        {
            Console.WriteLine($"[Inventory] destroy {ownerId} slot {slot} x{quantity} rejected");
            return true;
        }

        Console.WriteLine($"[Inventory] destroyed {taken.Count} x {ItemTable.NameOf(ItemTable.IndexOf(taken.NameCrc))} from slot {slot}");
        return true;
    }

    internal static bool Read(BitStream bitStream, out int ownerId, out int slot, out int quantity)
    {
        ownerId = slot = quantity = 0;

        if (!bitStream.ReadFullWidthUInt32(out var owner) || !bitStream.ReadByte(SlotMax, out var slotByte) || bitStream.GetNumberOfUnreadBits() < 1)
            return false;

        ownerId = (int)owner;
        slot = slotByte;

        if (!bitStream.ReadBit())
            return bitStream.ReadInt32(SmallQuantityMax, out quantity);

        if (!bitStream.ReadFullWidthUInt32(out var large))
            return false;

        quantity = (int)large;
        return true;
    }
}
