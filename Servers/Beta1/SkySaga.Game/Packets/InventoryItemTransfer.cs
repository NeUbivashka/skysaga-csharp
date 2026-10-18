namespace SkySaga.Game.Packets;

public class InventoryItemTransfer : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.InventoryItemTransfer);

        return bitStream;
    }
}
