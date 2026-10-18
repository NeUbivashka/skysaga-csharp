namespace SkySaga.Game.Packets;

public class DeleteNotification : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DeleteNotification);

        return bitStream;
    }
}
