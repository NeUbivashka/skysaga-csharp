namespace SkySaga.Game.Packets;

public class ExtraHomeIslandCreated : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.ExtraHomeIslandCreated);

        return bitStream;
    }
}
