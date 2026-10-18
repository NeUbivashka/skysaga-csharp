namespace SkySaga.Game.Packets;

public class EntityAdd : ISerializablePacket
{
    private const byte Ordinal = 153;

    private const uint ShortLengthMax = 0x20000;

    public uint? NameHash;

    public int Id;

    public int? ParentId;

    public required BitStream SyncData;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(Ordinal);

        bitStream.WriteOptional(NameHash, (value) =>
        {
            bitStream.Write((int)value);
        });

        bitStream.Write(Id);

        bitStream.WriteOptional(ParentId, bitStream.Write);

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
