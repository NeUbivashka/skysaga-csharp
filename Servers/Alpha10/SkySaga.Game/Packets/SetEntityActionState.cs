namespace SkySaga.Game.Packets;

public class SetEntityActionState : ISerializablePacket
{
    private const uint StateMax = 4;

    public int EntityId;
    public uint? ActionId;
    public uint State;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.SetEntityActionState);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        InventoryItemRef.WriteOptionalUInt32(bitStream, ActionId);
        bitStream.WriteRanged(Math.Min(State, StateMax), StateMax);

        return bitStream;
    }

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadFullWidthUInt32(out var entityId)) return false;
        if (!ReadOptionalUInt32(bitStream, out var actionId)) return false;
        if (!bitStream.ReadInt32(StateMax, out var state)) return false;

        Console.WriteLine($"[SetEntityActionState] entity {entityId} action {actionId} state {state}");

        connection.PlayerConnectionManager.BroadcastToAllExcept(
            connection.Guid.g,
            new SetEntityActionState
            {
                EntityId = (int)entityId,
                ActionId = actionId,
                State = (uint)state
            });

        return true;
    }

    private static bool ReadOptionalUInt32(BitStream bitStream, out uint? value)
    {
        value = null;
        if (!bitStream.ReadInt32(1, out var present)) return false;
        if (present == 0) return true;
        if (!bitStream.ReadFullWidthUInt32(out var v)) return false;
        value = v;
        return true;
    }
}
