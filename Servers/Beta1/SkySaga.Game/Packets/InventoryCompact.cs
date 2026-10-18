namespace SkySaga.Game.Packets;

public class InventoryCompact : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.InventoryCompact);

        return bitStream;
    }
}
