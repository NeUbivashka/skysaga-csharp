namespace SkySaga.Game.Packets;

public class DebugLearnRecipe : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugLearnRecipe);

        return bitStream;
    }
}
