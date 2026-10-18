namespace SkySaga.Game.Packets;

public class SealedDoorOpenedNotification : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SealedDoorOpenedNotification);

        return bitStream;
    }
}
