using System.Linq;

namespace SkySaga.Game.Packets;

public static class InventoryArrange
{
    private const byte CountMax = 0xF0;
    private const byte SlotMax = 0xF9;

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadFullWidthUInt32(out var entityId) || !bitStream.ReadByte(CountMax, out var shortCount))
            return false;

        uint count = shortCount;

        if (shortCount == CountMax && bitStream.ReadBit() && !bitStream.ReadFullWidthUInt32(out count))
            return false;

        var order = new int[count];

        for (var i = 0; i < count; i++)
        {
            if (!bitStream.ReadByte(SlotMax, out var slot))
                return false;

            order[i] = slot;
        }

        if (!connection.WorldManager.EntityManager.TryGetEntity((int)entityId, out var owner)
            || owner.Id != connection.PlayerEntity.Id
            || !owner.TryGetComponent<ClientInventoryComponent>(out var inventory))
        {
            Console.WriteLine($"[Inventory] arrange of entity {entityId} rejected");
            return true;
        }

        var first = (int)ClientEquippedItemComponent.SlotCount;
        var list = inventory.InventoryEntityList;
        var occupied = Enumerable.Range(first, Math.Max(0, list.Count - first)).Where(slot => list[slot] != 0).ToHashSet();

        if (order.Length != occupied.Count || !occupied.SetEquals(order))
        {
            Console.WriteLine($"[Inventory] arrange rejected: [{string.Join(",", order)}] is not the occupied backpack slots [{string.Join(",", occupied.Order())}]");
            return true;
        }

        List<int> arranged = [.. list.Take(first), .. order.Select(slot => list[slot])];
        arranged.AddRange(Enumerable.Repeat(0, list.Count - arranged.Count));

        inventory.InventoryEntityList = arranged;

        Console.WriteLine($"[Inventory] arranged {count} backpack slots");
        return true;
    }
}
