namespace SkySaga.Game.Packets;

public class CraftingFailed : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.CraftingFailed);

        return bitStream;
    }
}
