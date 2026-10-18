namespace SkySaga.Game.Packets;

public class InventoryItemTransferAll : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.InventoryItemTransferAll);

        return bitStream;
    }
}
