namespace SkySaga.Game.Packets;

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
