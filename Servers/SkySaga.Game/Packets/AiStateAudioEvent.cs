namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 290, server to client. Reader: FUN_007EAFC0 -> FUN_00BB3E50(1, 0x122, ...).
///
///     EntityId       32 bits, full width -> big-endian
///     AudioState     ranged, max 0xD
///
/// Field names are by role; the widths and the order come from the reader.
/// </summary>
public class AiStateAudioEvent : ISerializablePacket
{
    private const uint AudioStateMax = 0xD;
    public int EntityId;
    public uint AudioState;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.AiStateAudioEvent);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);
        bitStream.WriteRanged(Math.Min(AudioState, AudioStateMax), AudioStateMax);

        return bitStream;
    }
}
