namespace SkySaga.Game.Packets;

public class InventoryItemTransferToSlot : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.InventoryItemTransferToSlot);

        return bitStream;
    }
}
