namespace SkySaga.Game.Packets;

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
