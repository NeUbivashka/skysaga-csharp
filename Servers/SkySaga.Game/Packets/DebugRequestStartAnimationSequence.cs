namespace SkySaga.Game.Packets;

public class DebugRequestStartAnimationSequence : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugRequestStartAnimationSequence);

        return bitStream;
    }
}
