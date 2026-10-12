namespace SkySaga.Game.Packets;

public static class RequestEquipInventoryItem
{
    private const uint LocationMax = 9;
    private const uint InventorySlotMax = 0xF9;

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadInt32(LocationMax, out var location)) return false;
        if (!bitStream.ReadFullWidthUInt32(out var inventoryOwner)) return false;
        if (!bitStream.ReadInt32(InventorySlotMax, out var slotId)) return false;
        if (!bitStream.ReadInt32(1, out var fromUserCommand)) return false;
        if (!bitStream.ReadInt32(1, out var findSuitableSlot)) return false;

        Debug.WriteLine($"location {location}, inventoryOwner {inventoryOwner}, slotId {slotId}, fromUserCommand {fromUserCommand}, findSuitableSlot {findSuitableSlot}", nameof(RequestEquipInventoryItem));

        if (!connection.PlayerEntity.TryGetComponent<ClientInventoryComponent>(out var clientInventoryComponent)
            || slotId >= clientInventoryComponent.InventoryEntityList.Count
            || !connection.WorldManager.EntityManager.TryGetEntity(clientInventoryComponent.InventoryEntityList[slotId], out var itemEntity)
            || !itemEntity.TryGetComponent<InventoryItemComponent>(out var inventoryItemComponent))
            return true;

        var nameCrc = inventoryItemComponent.InventorySlotData.Name ?? 0;

        if (location < ClientEquippedItemComponent.SlotCount
            && connection.PlayerEntity.TryGetComponent<ClientEquippedItemComponent>(out var equipped))
        {
            equipped.SetSlot((uint)location, ItemTable.IndexOf(nameCrc),
                Guid.TryParse(inventoryItemComponent.InventorySlotData.ItemUUID, out var uuid) ? uuid : null,
                itemEntity.TryGetComponent<MaterialCompositionComponent>(out var composition) ? composition.MaterialList : null);
        }

        var blockID = BlockCrcMapping.GetBlockId(nameCrc);

        if (location == (int)ActionLocation.LeftHand)
            connection.LeftHandBlockId = blockID;
        else if (location == (int)ActionLocation.RightHand)
            connection.RightHandBlockId = blockID;

        Console.WriteLine($"[Equip] {(ClientEquippedItemComponent.Slot)location} = {ItemTable.NameOf(ItemTable.IndexOf(nameCrc))} (inventory slot {slotId})");

        return true;
    }
}
