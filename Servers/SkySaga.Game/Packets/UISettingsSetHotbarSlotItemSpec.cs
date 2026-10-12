namespace SkySaga.Game.Packets;

/// <summary>
/// RPCUISettingsSetHotbarSlotItemSpec, ordinal 18, client to server (send stub FUN_009C3BE0):
///
///   slot   ranged max 5 (3 bits)
///   hand   ranged max 2 (2 bits); 0 = left, 1 = right
///   item   item reference (writer FUN_0085A490, the counterpart of InventoryItemRef)
///
/// Stored on ClientSettingsComponent.HotbarItems, synced back through hotbarslotresources.
/// </summary>
public static class UISettingsSetHotbarSlotItemSpec
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadInt32(ClientSettingsComponent.LastActivatedSlotMax, out var slot)) return false;
        if (!bitStream.ReadInt32(2, out var hand)) return false;
        if (!InventoryItemRef.TryDecode(bitStream, out var item)) return false;

        if (connection.PlayerEntity.TryGetComponent<ClientSettingsComponent>(out var settings))
            settings.SetHotbarItem((uint)slot, (uint)hand, item);

        Console.WriteLine($"[Hotbar] slot {slot} hand {hand} = {ItemTable.NameOf(item.Kind)} ({item.Kind})");

        return true;
    }
}
