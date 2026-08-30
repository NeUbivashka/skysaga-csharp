namespace SkySaga.Game.Packets;

public class RequestUnEquipInventoryItem : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.RequestUnEquipInventoryItem);

        return bitStream;
    }
}
