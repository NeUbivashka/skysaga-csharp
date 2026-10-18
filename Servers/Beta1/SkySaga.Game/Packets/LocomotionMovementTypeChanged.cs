namespace SkySaga.Game.Packets;

public class LocomotionMovementTypeChanged : ISerializablePacket
{
    private const uint MovementTypeMax = 5;

    public int EntityId;
    public uint MovementType;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.LocomotionMovementTypeChanged);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        bitStream.WriteRanged(Math.Min(MovementType, MovementTypeMax), MovementTypeMax);

        return bitStream;
    }
}
