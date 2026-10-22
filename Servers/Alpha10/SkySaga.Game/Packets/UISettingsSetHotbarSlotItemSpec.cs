namespace SkySaga.Game.Packets;

public static class UISettingsSetHotbarSlotItemSpec
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadInt32(ClientSettingsComponent.LastActivatedSlotMax, out var slot)) return false;
        if (!bitStream.ReadInt32(2, out var hand)) return false;
        if (!InventoryItemRef.TryDecode(bitStream, out var item)) return false;

        if (connection.PlayerEntity.TryGetComponent<ClientSettingsComponent>(out var settings) && hand <= 1)
        {
            var otherHand = (uint)(1 - hand);
            var other = settings.HotbarItems[slot * 2 + otherHand];

            settings.SetHotbarItem((uint)slot, (uint)hand, item);

            if (TwoHandedTable.Contains(item.Kind))
                settings.SetHotbarItem((uint)slot, otherHand, item);
            else if (other is not null && (TwoHandedTable.Contains(other.Kind) || item.ItemUuid is { } uuid && other.ItemUuid == uuid))
                settings.SetHotbarItem((uint)slot, otherHand, null);

            if (slot == (int)settings.LastActivatedSlot)
                connection.SyncEquippedHotbar();
        }

        Console.WriteLine($"[Hotbar] slot {slot} hand {hand} = {ItemTable.NameOf(item.Kind)} ({item.Kind})");

        return true;
    }
}
