namespace SkySaga.Game.Packets;

/// <summary>
/// RPCUISettingsSelectHotbarSlot, ordinal 19, client to server (send stub FUN_009C30B0): the slot,
/// ranged max 5 (3 bits). Stored as ClientSettingsComponent.LastActivatedSlot.
/// </summary>
public static class UISettingsSelectHotbarSlot
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadInt32(ClientSettingsComponent.LastActivatedSlotMax, out var slot)) return false;

        if (connection.PlayerEntity.TryGetComponent<ClientSettingsComponent>(out var settings))
            settings.LastActivatedSlot = (uint)slot;

        return true;
    }
}
