namespace SkySaga.Game.Packets;

public class DebugSetBuildOffState : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugSetBuildOffState);

        return bitStream;
    }
}
