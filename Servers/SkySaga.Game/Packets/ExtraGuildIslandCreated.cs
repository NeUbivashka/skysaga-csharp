namespace SkySaga.Game.Packets;

public class ExtraGuildIslandCreated : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.ExtraGuildIslandCreated);

        return bitStream;
    }
}
