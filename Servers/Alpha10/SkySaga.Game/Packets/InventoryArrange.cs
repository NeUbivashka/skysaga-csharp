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

        if (first + count > list.Count
            || order.Distinct().Count() != order.Length
            || order.Any(slot => slot < first || slot >= first + count))
        {
            Console.WriteLine($"[Inventory] arrange rejected: [{string.Join(",", order)}] is not a permutation of slots {first}..{first + count - 1}");
            return true;
        }

        List<int> arranged = [.. list];

        for (var i = 0; i < count; i++)
            arranged[first + i] = list[order[i]];

        inventory.InventoryEntityList = arranged;

        Console.WriteLine($"[Inventory] arranged {count} backpack slots");
        return true;
    }
}
