namespace SkySaga.Game.Packets;

public static class RequestUnEquipInventoryItem
{
    private const uint LocationMax = 9;

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadInt32(LocationMax, out var location)) return false;
        if (!bitStream.ReadInt32(1, out _)) return false;

        if (location < ClientEquippedItemComponent.SlotCount
            && connection.PlayerEntity.TryGetComponent<ClientEquippedItemComponent>(out var equipped))
            equipped.SetSlot((uint)location, 0, null);

        if (location > (int)ClientEquippedItemComponent.Slot.RightHand
            && location < ClientEquippedItemComponent.SlotCount
            && connection.PlayerEntity.TryGetComponent<ClientInventoryComponent>(out var inventory)
            && location < inventory.InventoryEntityList.Count
            && inventory.InventoryEntityList[location] != 0)
        {
            var list = new List<int>(inventory.InventoryEntityList);
            var free = list.IndexOf(0, (int)ClientEquippedItemComponent.SlotCount);

            if (free >= 0)
            {
                (list[free], list[location]) = (list[location], 0);
                inventory.InventoryEntityList = list;
                Console.WriteLine($"[Equip] {(ClientEquippedItemComponent.Slot)location} unequipped to inventory slot {free}");
            }
        }

        if (location == (int)ActionLocation.LeftHand)
            connection.LeftHandBlockId = null;
        else if (location == (int)ActionLocation.RightHand)
            connection.RightHandBlockId = null;

        return true;
    }
}
