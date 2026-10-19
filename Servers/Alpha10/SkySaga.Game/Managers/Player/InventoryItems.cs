namespace SkySaga.Game.Managers.Player;

public static class InventoryItems
{
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
