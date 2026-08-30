namespace SkySaga.Game.Packets;

public class PromotionAddedToIsland : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PromotionAddedToIsland);

        return bitStream;
    }
}
