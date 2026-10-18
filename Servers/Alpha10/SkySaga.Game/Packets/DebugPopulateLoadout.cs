namespace SkySaga.Game.Packets;

public class DebugPopulateLoadout : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugPopulateLoadout);

        return bitStream;
    }
}
