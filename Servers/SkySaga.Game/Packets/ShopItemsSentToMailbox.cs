namespace SkySaga.Game.Packets;

public class ShopItemsSentToMailbox : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.ShopItemsSentToMailbox);

        return bitStream;
    }
}
