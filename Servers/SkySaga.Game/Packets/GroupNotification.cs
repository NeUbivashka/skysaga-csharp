namespace SkySaga.Game.Packets;

public class GroupNotification : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.GroupNotification);

        return bitStream;
    }
}
