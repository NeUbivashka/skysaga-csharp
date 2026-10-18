namespace SkySaga.Game.Packets;

public class SetIslandPublished : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetIslandPublished);

        return bitStream;
    }
}
