namespace SkySaga.Game.Packets;

public class HealOccurred : ISerializablePacket
{
    private const uint AmountMax = 0x7FFF;
    private const uint KindMax = 9;

    public int TargetEntityId;

    public int SourceEntityId;

    public uint Amount;

    public uint Kind;

    public int Unnamed;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.HealOccurred);

        WriteInt32(bitStream, TargetEntityId);
        WriteInt32(bitStream, SourceEntityId);

        bitStream.WriteRanged(Math.Min(Amount, AmountMax), AmountMax);
        bitStream.WriteRanged(Math.Min(Kind, KindMax), KindMax);

        WriteInt32(bitStream, Unnamed);

        return bitStream;
    }

    private static void WriteInt32(BitStream bitStream, int value)
    {
        var bytes = BitConverter.GetBytes(value);

        Array.Reverse(bytes);

        bitStream.WriteBits(bytes, 32, true);
    }
}
