namespace SkySaga.Game.Packets;

public class DebugAnimationSequenceCatchup : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugAnimationSequenceCatchup);

        return bitStream;
    }
}
