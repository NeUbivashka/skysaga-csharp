namespace SkySaga.Game.Packets;

public class RemoteMailSynced : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.RemoteMailSynced);

        return bitStream;
    }
}
