namespace SkySaga.Game.Packets;

public class InventoryItemDestroy : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.InventoryItemDestroy);

        return bitStream;
    }
}
