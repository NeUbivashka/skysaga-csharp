namespace SkySaga.Game.Managers.Player;

public static class InventoryItems
{
    public readonly record struct Taken(uint NameCrc, int Count, uint?[] Materials);

    public static Taken? Take(PlayerConnection connection, int ownerId, int slot, int quantity)
    {
        var entityManager = connection.WorldManager.EntityManager;

        if (ownerId != connection.PlayerEntity.Id
            || !connection.PlayerEntity.TryGetComponent<ClientInventoryComponent>(out var inventory)
            || slot < 0 || slot >= inventory.InventoryEntityList.Count
            || !entityManager.TryGetEntity(inventory.InventoryEntityList[slot], out var item)
            || !item.TryGetComponent<InventoryItemComponent>(out var component)
            || component.InventorySlotData.Name is not uint nameCrc
            || quantity <= 0)
            return null;

        var count = Math.Min(quantity, component.InventorySlotData.Count);
        var materials = ResourcePickups.MaterialsOf(item);

        if (component.InventorySlotData.Count > count)
        {
            component.InventorySlotData = component.InventorySlotData.WithCount(component.InventorySlotData.Count - count);
            return new Taken(nameCrc, count, materials);
        }

        List<int> list = [.. inventory.InventoryEntityList];
        list[slot] = 0;
        inventory.InventoryEntityList = list;

        entityManager.RemoveEntity(item.Id);
        connection.PlayerConnectionManager.BroadcastRemovedAfterSync(item.Id);

        return new Taken(nameCrc, count, materials);
    }

    public static bool Give(PlayerConnection connection, uint nameCrc, int count, uint?[]? materials = null)
    {
        if (!connection.PlayerEntity.TryGetComponent<ClientInventoryComponent>(out var inventory))
            return false;

        var entityManager = connection.WorldManager.EntityManager;

        if (PlayerInitializer.CreateInventoryItem(entityManager, nameCrc, count, materialOverrides: materials) is not { } item)
            return false;

        if (ResourcePickups.TryFindStack(entityManager, inventory, nameCrc, ResourcePickups.MaterialsOf(item), count, out var stack))
        {
            entityManager.RemoveEntity(item.Id);
            stack.InventorySlotData = stack.InventorySlotData.WithCount(stack.InventorySlotData.Count + count);
            return true;
        }

        var free = inventory.InventoryEntityList.IndexOf(0, (int)ClientEquippedItemComponent.SlotCount);

        if (free < 0)
        {
            entityManager.RemoveEntity(item.Id);
            return false;
        }

        List<int> list = [.. inventory.InventoryEntityList];
        list[free] = item.Id;
        inventory.InventoryEntityList = list;

        connection.PlayerConnectionManager.BroadcastToAll(new EntityAdd
        {
            Id = item.Id,
            NameHash = Util.ComputeCrc32(item.Name),
            SyncData = item.GetSyncData(newEntity: true)
        });

        return true;
    }
}
