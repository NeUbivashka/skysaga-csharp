namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 106, server to client. Reader: FUN_007F6E70 -> FUN_00BB3E50(1, 0x6A, ...).
///
///     EntityId       32 bits, full width -> big-endian
///     MovementType   ranged, max 5 -> 3 bits
/// </summary>
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
