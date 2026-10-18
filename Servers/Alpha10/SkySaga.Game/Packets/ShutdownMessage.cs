namespace SkySaga.Game.Packets;

public class ShutdownMessage : ISerializablePacket
{
    public uint Reason;

    public uint Seconds;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.ShutdownMessage);

        WriteUInt32(bitStream, Reason);
        WriteUInt32(bitStream, Seconds);

        return bitStream;
    }

    private static void WriteUInt32(BitStream bitStream, uint value)
    {
        var bytes = BitConverter.GetBytes(value);

        Array.Reverse(bytes);

        bitStream.WriteBits(bytes, 32, true);
    }
}
