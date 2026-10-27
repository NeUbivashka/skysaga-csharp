namespace SkySaga.Game.Packets;

public static class EntityPerformedAbilityReceived
{
    private const uint PowerMax = 0x20;
    private const uint ExtentXMax = 0x400;
    private const uint ExtentYMax = 0xE0;
    private const uint ExtentZMax = 0x400;

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadFullWidthUInt32(out var entityId)) return false;
        if (!bitStream.ReadFullWidthUInt32(out var itemId)) return false;

        connection.LastUsedItemId = (int)itemId;
        PlayerActionRelay.Relay(connection, bitStream, entityId);

        if (!RunActionListReceived.ReadOptionalUInt32(bitStream, out _)) return true;
        if (!RunActionListReceived.ReadOptionalUInt32(bitStream, out _)) return true;
        if (!RunActionListReceived.ReadPosition(bitStream, out _)) return true;
        if (!RunActionListReceived.ReadDirection(bitStream, out _)) return true;
        if (!bitStream.ReadInt32(PowerMax, out _)) return true;
        if (!bitStream.ReadInt32(1, out _)) return true;
        if (!bitStream.ReadInt32(ExtentXMax, out _)) return true;
        if (!bitStream.ReadInt32(ExtentYMax, out _)) return true;
        if (!bitStream.ReadInt32(ExtentZMax, out _)) return true;
        if (!bitStream.ReadFullWidthUInt32(out _)) return true;
        if (!RunActionListReceived.ReadPosition(bitStream, out _)) return true;
        if (!RunActionListReceived.ReadDirection(bitStream, out _)) return true;
        if (!RunActionListReceived.ReadOptionalUInt32(bitStream, out var actionCrc) || actionCrc is not uint crc) return true;

        if (ActionStatTable.TryGet(crc, out var action))
            connection.Ai.PlayerActionStarted(connection.PlayerEntity, action);

        return true;
    }
}
