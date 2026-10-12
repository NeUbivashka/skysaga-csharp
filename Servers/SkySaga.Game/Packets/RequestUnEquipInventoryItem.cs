namespace SkySaga.Game.Packets;

/// <summary>
/// RPCRequestUnEquipInventoryItem, ordinal 16, client to server (send stub FUN_00956F70):
///
///   location          equipment slot, ranged max 9 (4 bits)
///   fromUserCommand   1 bit
///
/// Clears that slot of equippeditemspecs.
/// </summary>
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

        if (location == (int)ActionLocation.LeftHand)
            connection.LeftHandBlockId = null;
        else if (location == (int)ActionLocation.RightHand)
            connection.RightHandBlockId = null;

        return true;
    }
}
