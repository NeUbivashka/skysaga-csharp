namespace SkySaga.Game.Packets;

public class DebugForgetRecipe : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugForgetRecipe);

        return bitStream;
    }
}
