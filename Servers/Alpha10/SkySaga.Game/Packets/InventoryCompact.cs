using System.Linq;

namespace SkySaga.Game.Packets;

public static class InventoryCompact
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadFullWidthUInt32(out var entityId))
            return false;

        if (entityId != connection.PlayerEntity.Id
            || !connection.PlayerEntity.TryGetComponent<ClientInventoryComponent>(out var inventory))
        {
            Console.WriteLine($"[Inventory] compact of entity {entityId} not supported");
            return true;
        }

        var entityManager = connection.WorldManager.EntityManager;
        var first = (int)ClientEquippedItemComponent.SlotCount;
        List<int> list = [.. inventory.InventoryEntityList];
        var merged = 0;

        for (var source = list.Count - 1; source > first; source--)
        {
            if (!TryGetStack(entityManager, list[source], out var sourceItem, out var sourceStack))
                continue;

            for (var target = first; target < source && sourceStack.InventorySlotData.Count > 0; target++)
            {
                if (!TryGetStack(entityManager, list[target], out var targetItem, out var targetStack)
                    || targetStack.InventorySlotData.Name != sourceStack.InventorySlotData.Name
                    || !ResourcePickups.MaterialsOf(targetItem).SequenceEqual(ResourcePickups.MaterialsOf(sourceItem)))
                    continue;

                var room = StackLimitTable.Get(targetStack.InventorySlotData.Name) - targetStack.InventorySlotData.Count;
                var moved = Math.Min(room, sourceStack.InventorySlotData.Count);

                if (moved <= 0)
                    continue;

                targetStack.InventorySlotData = targetStack.InventorySlotData.WithCount(targetStack.InventorySlotData.Count + moved);
                sourceStack.InventorySlotData = sourceStack.InventorySlotData.WithCount(sourceStack.InventorySlotData.Count - moved);
                merged += moved;
            }

            if (sourceStack.InventorySlotData.Count > 0)
                continue;

            list[source] = 0;
            entityManager.RemoveEntity(sourceItem.Id);
            connection.PlayerConnectionManager.BroadcastRemovedAfterSync(sourceItem.Id);
        }

        inventory.InventoryEntityList = list;

        Console.WriteLine($"[Inventory] compacted backpack, {merged} items merged");
        return true;
    }

    private static bool TryGetStack(IMapEntityManager entityManager, int entityId, out Entity item, out InventoryItemComponent stack)
    {
        stack = null!;
        item = null!;

        return entityId != 0
            && entityManager.TryGetEntity(entityId, out item!)
            && item.TryGetComponent(out stack!)
            && stack.InventorySlotData.Name is not null;
    }
}
