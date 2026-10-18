namespace SkySaga.Game.Packets;

public class SetEmoteBinding : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetEmoteBinding);

        return bitStream;
    }
}
