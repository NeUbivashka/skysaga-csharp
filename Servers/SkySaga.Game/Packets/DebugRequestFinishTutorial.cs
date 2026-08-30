namespace SkySaga.Game.Packets;

public class DebugRequestFinishTutorial : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugRequestFinishTutorial);

        return bitStream;
    }
}
