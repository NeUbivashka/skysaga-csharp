namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 88, server to client. Reader: FUN_00800ED0 -> FUN_00BB3E50(1, 0x58, ...).
///
///     EntityId      32 bits, full width -> big-endian
///     Temperature   FUN_007D6490: ranged, max 0x40 -> 7 bits
/// </summary>
public class TemperaturePing : ISerializablePacket
{
    private const uint TemperatureMax = 0x40;

    public int EntityId;
    public uint Temperature;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.TemperaturePing);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        bitStream.WriteRanged(Math.Min(Temperature, TemperatureMax), TemperatureMax);

        return bitStream;
    }
}
