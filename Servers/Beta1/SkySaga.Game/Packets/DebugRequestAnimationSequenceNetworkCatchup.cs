namespace SkySaga.Game.Packets;

public class DebugRequestAnimationSequenceNetworkCatchup : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugRequestAnimationSequenceNetworkCatchup);

        return bitStream;
    }
}
