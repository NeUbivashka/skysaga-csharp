namespace SkySaga.Game.Packets;

public class PvPWaitingInfo : ISerializablePacket
{
    public ulong Value;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.PvPWaitingInfo);

        WriteUInt64(bitStream, Value);

        return bitStream;
    }

    internal static void WriteUInt64(BitStream bitStream, ulong value)
    {
        var bytes = BitConverter.GetBytes(value);

        Array.Reverse(bytes);

        bitStream.WriteBits(bytes, 64, true);
    }
}
