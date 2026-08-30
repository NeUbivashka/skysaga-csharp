namespace SkySaga.Game.Packets;

public class NewMailRecieved : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.NewMailRecieved);

        return bitStream;
    }
}
