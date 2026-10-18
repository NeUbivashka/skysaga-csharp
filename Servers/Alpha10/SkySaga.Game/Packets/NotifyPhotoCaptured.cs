namespace SkySaga.Game.Packets;

public class NotifyPhotoCaptured : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.NotifyPhotoCaptured);

        return bitStream;
    }
}
