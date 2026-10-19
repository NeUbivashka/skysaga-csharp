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

        if (location > (int)ClientEquippedItemComponent.Slot.RightHand
            && location < ClientEquippedItemComponent.SlotCount
            && location < clientInventoryComponent.InventoryEntityList.Count
            && slotId != location)
        {
            clientInventoryComponent.ApplyClientPredictedSwap(location, slotId);
        }

        var itemIndex = ItemTable.IndexOf(nameCrc);
        var isHand = location is (int)ClientEquippedItemComponent.Slot.LeftHand or (int)ClientEquippedItemComponent.Slot.RightHand;
        var twoHanded = isHand && TwoHandedTable.Contains(itemIndex);
        var blockID = BlockCrcMapping.GetBlockId(nameCrc);

        if (location < ClientEquippedItemComponent.SlotCount
            && connection.PlayerEntity.TryGetComponent<ClientEquippedItemComponent>(out var equipped))
        {
            var uuid = Guid.TryParse(inventoryItemComponent.InventorySlotData.ItemUUID, out var parsed) ? parsed : (Guid?)null;
            var materials = itemEntity.TryGetComponent<MaterialCompositionComponent>(out var composition) ? composition.MaterialList : null;
            var otherHand = 1 - location;

            if (twoHanded)
            {
                equipped.SetSlot((uint)ClientEquippedItemComponent.Slot.LeftHand, itemIndex, uuid, materials);
                equipped.SetSlot((uint)ClientEquippedItemComponent.Slot.RightHand, itemIndex, uuid, materials);
                connection.LeftHandBlockId = blockID;
                connection.RightHandBlockId = blockID;
            }
            else
            {
                equipped.SetSlot((uint)location, itemIndex, uuid, materials);

                if (isHand && TwoHandedTable.Contains(equipped.EquippedItems[otherHand]))
                {
                    equipped.SetSlot((uint)otherHand, 0, null);
                    SetHandBlock(connection, otherHand, null);
                }
            }
        }

        if (!twoHanded)
            SetHandBlock(connection, location, blockID);

        Console.WriteLine($"[Equip] {(ClientEquippedItemComponent.Slot)location} = {ItemTable.NameOf(ItemTable.IndexOf(nameCrc))} (inventory slot {slotId})");

        return true;
    }

    private static void SetHandBlock(PlayerConnection connection, int location, int? blockId)
    {
        if (location == (int)ActionLocation.LeftHand)
            connection.LeftHandBlockId = blockId;
        else if (location == (int)ActionLocation.RightHand)
            connection.RightHandBlockId = blockId;
    }
}
