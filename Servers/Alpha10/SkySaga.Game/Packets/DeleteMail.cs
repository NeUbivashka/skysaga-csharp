namespace SkySaga.Game.Packets;

public class DeleteMail : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DeleteMail);

        return bitStream;
    }
}
