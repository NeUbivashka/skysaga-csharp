namespace SkySaga.Game.Packets;

public class InventoryArrange : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.InventoryArrange);

        return bitStream;
    }
}
