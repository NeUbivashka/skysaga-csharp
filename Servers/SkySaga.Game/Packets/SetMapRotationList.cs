namespace SkySaga.Game.Packets;

public class SetMapRotationList : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetMapRotationList);

        return bitStream;
    }
}
