namespace SkySaga.Game.Packets;

public class EntityAdd : ISerializablePacket
{
    private const byte Ordinal = 153;



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






        bitStream.WriteBits(BitConverter.GetBytes(SyncData.GetNumberOfBitsUsed()), 32 - Util.NumBitsRequiredUInt32(0x20000), true);

        if (SyncData.GetNumberOfBitsUsed() > 0)
            bitStream.Write(SyncData);

        return bitStream;
    }
}