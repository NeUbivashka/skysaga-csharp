namespace SkySaga.Game.Packets;

public class EntitySync : ISerializablePacket
{



    private const byte Ordinal = 154;

    public int Id;

    public required BitStream SyncData;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(Ordinal);

        bitStream.Write(Id);

        bitStream.WriteBits(BitConverter.GetBytes(SyncData.GetNumberOfBitsUsed()), 32 - Util.NumBitsRequiredUInt32(0x20000));

        if (SyncData.GetNumberOfBitsUsed() > 0)
            bitStream.Write(SyncData);

        return bitStream;
    }
}