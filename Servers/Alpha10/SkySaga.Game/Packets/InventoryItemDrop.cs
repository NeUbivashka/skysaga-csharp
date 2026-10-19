namespace SkySaga.Game.Packets;

public static class InventoryItemDrop
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!InventoryItemDestroy.Read(bitStream, out var ownerId, out var slot, out var quantity))
            return false;

        if (InventoryItems.Take(connection, ownerId, slot, quantity) is not { } taken)
        {
            Console.WriteLine($"[Inventory] drop {ownerId} slot {slot} x{quantity} rejected");
            return true;
        }

        ResourcePickups.SpawnDropped(connection, taken.NameCrc, taken.Count, taken.Materials);

        Console.WriteLine($"[Inventory] dropped {taken.Count} x {ItemTable.NameOf(ItemTable.IndexOf(taken.NameCrc))} from slot {slot}");
        return true;
    }
}
