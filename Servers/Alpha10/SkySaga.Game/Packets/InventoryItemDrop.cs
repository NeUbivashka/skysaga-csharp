namespace SkySaga.Game.Packets;

public class InventoryItemDrop : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.InventoryItemDrop);

        return bitStream;
    }
}
