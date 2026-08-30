namespace SkySaga.Game.Packets;

public class DebugPauseLogicNetwork : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugPauseLogicNetwork);

        return bitStream;
    }
}
