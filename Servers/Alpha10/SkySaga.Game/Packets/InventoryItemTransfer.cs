using System.Linq;

namespace SkySaga.Game.Packets;

public static class InventoryItemTransfer
{
    private const uint SlotMax = 0xF9;
    private const uint SmallQuantityMax = 0x40;
    private const uint OptionMax = 9;

    public static bool Handle(PlayerConnection connection, BitStream bitStream) => Read(connection, bitStream, toSlot: false);

    public static bool HandleToSlot(PlayerConnection connection, BitStream bitStream) => Read(connection, bitStream, toSlot: true);

    private static bool Read(PlayerConnection connection, BitStream bitStream, bool toSlot)
    {
        if (!bitStream.ReadFullWidthUInt32(out var sourceId)) return false;
        if (!bitStream.ReadFullWidthUInt32(out var targetId)) return false;
        if (!bitStream.ReadInt32(SlotMax, out var slot)) return false;
        if (!ReadQuantity(bitStream, out var quantity)) return false;

        var targetSlot = -1;

        if (toSlot && !bitStream.ReadInt32(SlotMax, out targetSlot)) return false;
        if (!bitStream.ReadInt32(OptionMax, out var option)) return false;

        bitStream.ReadBit();
        bitStream.ReadBit();
        bitStream.ReadBit();

        Console.WriteLine($"[Transfer] {sourceId} slot {slot} x{quantity} -> {targetId} slot {targetSlot} (option {option})");

        Move(connection, (int)sourceId, (int)targetId, slot, quantity, targetSlot);

        // Requirement badges on Adventure cards are evaluated from the
        // rotation-list snapshot. Refresh it after a chest/player transfer so
        // coloured Keystone fragments and Realm Travelling Objects are counted.
        ClientReadyToPlay.SendAdventureRotation(connection, flushPlayerInventory: true);

        foreach (var id in new[] { (int)sourceId, (int)targetId })
            if (connection.WorldManager.EntityManager.TryGetEntity(id, out var entity))
                connection.WorldManager.DeviceInventoryChanged(entity);

        return true;
    }

    private static void Move(PlayerConnection connection, int sourceId, int targetId, int slot, int quantity, int targetSlot)
    {
        var entityManager = connection.WorldManager.EntityManager;

        if (!entityManager.TryGetEntity(sourceId, out var source)
            || !entityManager.TryGetEntity(targetId, out var target)
            || !source.TryGetComponent<ClientInventoryComponent>(out var from)
            || !target.TryGetComponent<ClientInventoryComponent>(out var to)
            || slot >= from.InventoryEntityList.Count
            || !entityManager.TryGetEntity(from.InventoryEntityList[slot], out var item)
            || !item.TryGetComponent<InventoryItemComponent>(out var itemComponent))
            return;

        var count = itemComponent.InventorySlotData.Count;

        Console.WriteLine($"[Transfer] item {item.Id} = {(itemComponent.InventorySlotData.Name is uint itemCrc ? ItemTable.NameOf(ItemTable.IndexOf(itemCrc)) : "?")} x{count}, materials [{string.Join(",", ResourcePickups.MaterialsOf(item))}]");
        var moved = Math.Clamp(quantity, 1, Math.Max(count, 1));
        var materials = ResourcePickups.MaterialsOf(item);
        var name = itemComponent.InventorySlotData.Name;

        if (targetSlot >= 0)
        {
            if (targetSlot >= to.InventoryEntityList.Count)
            {
                Console.WriteLine($"[Transfer] {target.Name}#{target.Id} target slot {targetSlot} is out of range");
                return;
            }

            if (ReferenceEquals(from, to) && slot == targetSlot)
                return;

            var targetItemId = to.InventoryEntityList[targetSlot];

            if (targetItemId != 0)
            {
                if (TryStackIntoOccupiedSlot(connection, from, slot, item, itemComponent, moved,
                        targetItemId, name, materials))
                    return;

                if (moved < count)
                {
                    Console.WriteLine($"[Transfer] cannot swap partial stack x{moved}/{count} into occupied slot {targetSlot}");
                    return;
                }

                SwapOccupiedSlots(source, target, from, to, slot, targetSlot, item.Id, targetItemId);
                return;
            }
        }

        if (targetSlot < 0 && ResourcePickups.TryFindStack(entityManager, to, name, materials, moved, out var stack))
        {
            stack.InventorySlotData = stack.InventorySlotData.WithCount(stack.InventorySlotData.Count + moved);
            TakeFromSource(connection, from, slot, item, itemComponent, moved);
            return;
        }

        var free = targetSlot >= 0
            ? targetSlot < to.InventoryEntityList.Count && to.InventoryEntityList[targetSlot] == 0 ? targetSlot : -1
            : to.InventoryEntityList.IndexOf(0, (int)ClientEquippedItemComponent.SlotCount);

        if (free < 0)
        {
            Console.WriteLine($"[Transfer] {target.Name}#{target.Id} has no free slot");
            return;
        }

        Entity placed;

        if (moved >= count)
        {
            placed = item;
            List<int> sourceList = [.. from.InventoryEntityList];
            sourceList[slot] = 0;
            from.InventoryEntityList = sourceList;
        }
        else
        {
            if (name is not uint crc
                || PlayerInitializer.CreateInventoryItem(entityManager, crc, moved, materialOverrides: materials) is not { } split)
                return;

            itemComponent.InventorySlotData = itemComponent.InventorySlotData.WithCount(count - moved);
            placed = split;
            connection.PlayerConnectionManager.BroadcastToAll(new EntityAdd
            {
                Id = split.Id,
                NameHash = Util.ComputeCrc32(split.Name),
                SyncData = split.GetSyncData(newEntity: true)
            });
        }

        List<int> targetList = [.. to.InventoryEntityList];
        targetList[free] = placed.Id;
        to.InventoryEntityList = targetList;

        Console.WriteLine($"[Transfer] {placed.Name}#{placed.Id} -> {target.Name}#{target.Id} slot {free}");
    }

    private static bool TryStackIntoOccupiedSlot(PlayerConnection connection, InventoryComponent from, int sourceSlot,
        Entity sourceItem, InventoryItemComponent sourceComponent, int moved, int targetItemId, uint? sourceName,
        uint?[] sourceMaterials)
    {
        var entityManager = connection.WorldManager.EntityManager;

        if (!entityManager.TryGetEntity(targetItemId, out var targetItem)
            || !targetItem.TryGetComponent<InventoryItemComponent>(out var targetComponent)
            || targetComponent.InventorySlotData.Name != sourceName
            || !ResourcePickups.MaterialsOf(targetItem).SequenceEqual(sourceMaterials))
            return false;

        var available = StackLimitTable.Get(sourceName) - targetComponent.InventorySlotData.Count;

        if (available <= 0)
            return false;

        var stacked = Math.Min(moved, available);
        targetComponent.InventorySlotData = targetComponent.InventorySlotData.WithCount(
            targetComponent.InventorySlotData.Count + stacked);
        TakeFromSource(connection, from, sourceSlot, sourceItem, sourceComponent, stacked);

        Console.WriteLine($"[Transfer] stacked x{stacked} into {targetItem.Name}#{targetItem.Id}, now {targetComponent.InventorySlotData.Count}");
        return true;
    }

    private static void SwapOccupiedSlots(Entity source, Entity target, InventoryComponent from, InventoryComponent to,
        int sourceSlot, int targetSlot, int sourceItemId, int targetItemId)
    {
        if (ReferenceEquals(from, to))
        {
            List<int> list = [.. from.InventoryEntityList];
            (list[sourceSlot], list[targetSlot]) = (list[targetSlot], list[sourceSlot]);
            from.InventoryEntityList = list;
        }
        else
        {
            List<int> sourceList = [.. from.InventoryEntityList];
            List<int> targetList = [.. to.InventoryEntityList];
            sourceList[sourceSlot] = targetItemId;
            targetList[targetSlot] = sourceItemId;
            from.InventoryEntityList = sourceList;
            to.InventoryEntityList = targetList;
        }

        Console.WriteLine($"[Transfer] swapped {sourceItemId} in {source.Name}#{source.Id} slot {sourceSlot} "
            + $"with {targetItemId} in {target.Name}#{target.Id} slot {targetSlot}");
    }

    private static void TakeFromSource(PlayerConnection connection, InventoryComponent from, int slot, Entity item, InventoryItemComponent itemComponent, int moved)
    {
        if (itemComponent.InventorySlotData.Count > moved)
        {
            itemComponent.InventorySlotData = itemComponent.InventorySlotData.WithCount(itemComponent.InventorySlotData.Count - moved);
            return;
        }

        List<int> sourceList = [.. from.InventoryEntityList];
        sourceList[slot] = 0;
        from.InventoryEntityList = sourceList;

        connection.WorldManager.EntityManager.RemoveEntity(item.Id);
        connection.PlayerConnectionManager.BroadcastRemovedAfterSync(item.Id);
    }

    private static bool ReadQuantity(BitStream bitStream, out int quantity)
    {
        quantity = 0;

        if (!bitStream.ReadInt32(1, out var large)) return false;

        if (large == 0)
            return bitStream.ReadInt32(SmallQuantityMax, out quantity);

        if (!bitStream.ReadFullWidthUInt32(out var value)) return false;

        quantity = (int)value;
        return true;
    }
}
