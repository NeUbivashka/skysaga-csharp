namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 72, server to client. Reader: FUN_008012D0 -> FUN_00BB3E50(1, 0x48, ...).
///
///     Timestamp   32 bits, full width -> big-endian
///     Phase       FUN_007D7860: ranged, max 0xF -> 4 bits
///     Unnamed     32 bits, full width -> big-endian
///
/// Three fields in that order. Names are by role - two full words around a small counter is what
/// a clock message looks like, but nothing here names itself.
/// </summary>
public class TimeSync : ISerializablePacket
{
    private const uint PhaseMax = 0xF;

    public uint Timestamp;

    public uint Phase;

    public uint Unnamed;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.TimeSync);

        InventoryItemRef.WriteUInt32(bitStream, Timestamp);

        bitStream.WriteRanged(Math.Min(Phase, PhaseMax), PhaseMax);

        InventoryItemRef.WriteUInt32(bitStream, Unnamed);

        return bitStream;
    }
}
