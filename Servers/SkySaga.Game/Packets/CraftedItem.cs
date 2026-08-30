namespace SkySaga.Game.Packets;

public class CraftedItem : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.CraftedItem);

        return bitStream;
    }
}
