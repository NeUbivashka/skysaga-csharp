namespace SkySaga.Game.Packets;

public class StorageItemSell : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.StorageItemSell);

        return bitStream;
    }
}
