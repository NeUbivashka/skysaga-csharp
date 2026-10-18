namespace SkySaga.Game.Packets;

public class EntitySync : ISerializablePacket
{

    private const uint ShortLengthMax = 0x20000;

    public int Id;

    public required BitStream SyncData;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.EntitySync);

        bitStream.Write(Id);

        var syncDataBits = SyncData.GetNumberOfBitsUsed();

        WriteSyncDataLength(bitStream, syncDataBits);

        if (syncDataBits > 0)
        {
            SyncData.ResetReadPointer();
            bitStream.Write(SyncData, syncDataBits);
        }

        return bitStream;
    }

    private static void WriteSyncDataLength(BitStream bitStream, uint bits)
    {
        if (bits <= ShortLengthMax)
        {
            bitStream.Write0();

            bitStream.WriteBits(
                BitConverter.GetBytes(bits),
                32 - Util.NumBitsRequiredUInt32(ShortLengthMax),
                true);

            return;
        }

        bitStream.Write1();

        bitStream.WriteBits(BitConverter.GetBytes(bits), 32, true);
    }
}
