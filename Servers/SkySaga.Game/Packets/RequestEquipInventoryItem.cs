namespace SkySaga.Game.Packets;

/// <summary>
/// RPCRequestEquipInventoryItem, ordinal 15, client to server (send stub FUN_00956A20). Sent when
/// the player picks a hotbar slot or equips from the inventory:
///
///   location            equipment slot, ranged max 9 (4 bits): ClientEquippedItemComponent.Slot
///   inventoryOwner      full-width u32, the entity holding the inventory (the player)
///   inventorySlot       ranged max 0xF9 (8 bits, FUN_008575B0)
///   fromUserCommand     1 bit
///   findSuitableSlot    1 bit
///
/// Example: "10 00 00 00 51 08" = right hand, owner 5, slot 16, from the user. The client only
/// holds the item once equippeditemspecs says so, so the server applies the request there.
/// </summary>
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
                Guid.TryParse(inventoryItemComponent.InventorySlotData.ItemUUID, out var uuid) ? uuid : null);
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
