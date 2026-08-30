namespace SkySaga.Game.Packets;

public class DebugStepLogicNetwork : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugStepLogicNetwork);

        return bitStream;
    }
}
