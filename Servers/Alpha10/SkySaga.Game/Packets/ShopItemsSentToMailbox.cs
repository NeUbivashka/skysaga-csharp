namespace SkySaga.Game.Packets;

public class ShopItemsSentToMailbox : ISerializablePacket
{
    public int MailId;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.ShopItemsSentToMailbox);

        InventoryItemRef.WriteUInt32(bitStream, (uint)MailId);

        return bitStream;
    }
}
