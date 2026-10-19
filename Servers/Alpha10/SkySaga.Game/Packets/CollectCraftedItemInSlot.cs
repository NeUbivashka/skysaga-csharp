namespace SkySaga.Game.Packets;

public static class CollectCraftedItemInSlot
{
    private const uint SlotMax = 12;

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadFullWidthUInt32(out var entityId)) return false;
        if (!bitStream.ReadInt32(SlotMax, out var slot)) return false;
        if (bitStream.GetNumberOfUnreadBits() < 2) return false;

        var first = bitStream.ReadBit();
        var second = bitStream.ReadBit();

        Crafting.Collect(connection, (int)entityId, slot, first, second);
        return true;
    }
}
