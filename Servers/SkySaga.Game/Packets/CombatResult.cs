namespace SkySaga.Game.Packets;

public class CombatResult : ISerializablePacket
{
    private const uint ResultMax = 0x98;

    public uint Result;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.CombatResult);

        bitStream.WriteRanged(Math.Min(Result, ResultMax), ResultMax);

        return bitStream;
    }
}
