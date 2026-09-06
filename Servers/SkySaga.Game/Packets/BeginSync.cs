namespace SkySaga.Game.Packets;

/// <summary>
/// Reader: FUN_007EC550 -> FUN_00BB3E50(1, 0xC, ...), field read by FUN_007D72F0.
///
/// One ranged field, maximum 0x1C00, so 13 bits, clamped to 0x1C00 on read. The server was
/// writing it with a maximum of 0x8000 - 16 bits.
///
/// This has been latent rather than fatal. With 16 chunks the whole value sits in the first
/// byte, and because the reader right-aligns only the trailing partial byte, the client still
/// came out with 16. It breaks above 255: 300 chunks would arrive as 44, because the second
/// byte of the little-endian value falls outside the 13 bits the client takes. Nothing rides on
/// the three extra bits themselves - BeginSync carries no other field - so only the value was
/// ever at risk.
/// </summary>
public class BeginSync : ISerializablePacket
{
    private const uint NumChunksMax = 0x1C00;

    public int NumChunksToSync;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.BeginSync);

        var clamped = Math.Min((uint)NumChunksToSync, NumChunksMax);

        bitStream.WriteRanged(clamped, NumChunksMax);

        return bitStream;
    }
}
