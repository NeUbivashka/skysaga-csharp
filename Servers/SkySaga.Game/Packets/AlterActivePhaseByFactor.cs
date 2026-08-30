namespace SkySaga.Game.Packets;

public class AlterActivePhaseByFactor : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.AlterActivePhaseByFactor);

        return bitStream;
    }
}
