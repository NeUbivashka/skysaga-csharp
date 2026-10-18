namespace SkySaga.Game.Packets;

public class SentErrorToClient : ISerializablePacket
{
    public uint ErrorCode;

    public uint Detail;

    public string? Title;

    public string? Message;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SentErrorToClient);

        WriteUInt32(bitStream, ErrorCode);
        WriteUInt32(bitStream, Detail);

        bitStream.WriteString(Title);
        bitStream.WriteString(Message);

        return bitStream;
    }

    private static void WriteUInt32(BitStream bitStream, uint value)
    {
        var bytes = BitConverter.GetBytes(value);

        Array.Reverse(bytes);

        bitStream.WriteBits(bytes, 32, true);
    }
}
