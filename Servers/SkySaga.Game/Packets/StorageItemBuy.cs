namespace SkySaga.Game.Packets;

public class StorageItemBuy : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.StorageItemBuy);

        return bitStream;
    }
}
