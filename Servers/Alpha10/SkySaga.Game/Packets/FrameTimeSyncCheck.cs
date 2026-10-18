namespace SkySaga.Game.Packets;

public class FrameTimeSyncCheck : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.FrameTimeSyncCheck);

        return bitStream;
    }
}
