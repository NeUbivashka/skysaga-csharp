namespace SkySaga.Game.Packets;

public class DebugSetInteractionEnabled : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugSetInteractionEnabled);

        return bitStream;
    }
}
