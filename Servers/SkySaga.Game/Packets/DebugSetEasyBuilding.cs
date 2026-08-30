namespace SkySaga.Game.Packets;

public class DebugSetEasyBuilding : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugSetEasyBuilding);

        return bitStream;
    }
}
