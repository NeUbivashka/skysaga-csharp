namespace SkySaga.Game.Packets;

public static class RequestEquipInventoryItem
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!ReadLocation(bitStream, out var location)) return false;

        if (!bitStream.Read(out int inventoryOwner)) return false;

        if(!bitStream.ReadInt32(45, out var slotId)) return false;

        var fromUserCommand = bitStream.ReadBit();
        var findSuitableSlot = bitStream.ReadBit();

        Debug.WriteLine($"location {location}, inventoryOwner {inventoryOwner}, slotId {slotId}, fromUserCommand {fromUserCommand}, findSuitableSlot {findSuitableSlot}", nameof(RequestEquipInventoryItem));

        if (connection.PlayerEntity.TryGetComponent<ClientInventoryComponent>(out var clientInventoryComponent))
        {
            var itemId = clientInventoryComponent.InventoryEntityList[slotId];
            connection.WorldManager.EntityManager.TryGetEntity(itemId, out var itemEntity);

            if (itemEntity!.TryGetComponent<InventoryItemComponent>(out var inventoryItemComponent))
            {
                var blockID = BlockCrcMapping.GetBlockId(inventoryItemComponent.InventorySlotData.Name ?? 0);
                if (location == ActionLocation.LeftHand)
                {
                    connection.LeftHandBlockId = blockID;
                }
                else if (location == ActionLocation.RightHand)
                {
                    connection.RightHandBlockId = blockID;
                }
            }
        }

        return true;
    }

    private static bool ReadLocation(BitStream bitStream, out ActionLocation location)
    {
        if (!bitStream.ReadByte(8, out var tmpLocation))
        {
            location = 0;
            return false;
        }
        location = (ActionLocation)tmpLocation;
        return true;
    }
}
