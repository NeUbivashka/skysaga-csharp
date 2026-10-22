namespace SkySaga.Game.Packets;

public static class UISettingsSelectHotbarSlot
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadInt32(ClientSettingsComponent.LastActivatedSlotMax, out var slot)) return false;

        if (connection.PlayerEntity.TryGetComponent<ClientSettingsComponent>(out var settings))
        {
            settings.LastActivatedSlot = (uint)slot;
            connection.SyncEquippedHotbar();
        }

        return true;
    }
}
