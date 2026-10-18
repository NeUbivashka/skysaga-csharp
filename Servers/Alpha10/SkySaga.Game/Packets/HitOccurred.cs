namespace SkySaga.Game.Packets;

public class HitOccurred : ISerializablePacket
{
    private const uint AmountMax = 0x7FFF;
    private const uint KindMax = 9;
    private const uint ResultMax = 0x98;

    public int TargetEntityId;
    public int SourceEntityId;
    public uint Amount;
    public uint Kind;
    public int Unnamed;

    public uint? Result;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.HitOccurred);

        InventoryItemRef.WriteUInt32(bitStream, (uint)SourceEntityId);
        InventoryItemRef.WriteUInt32(bitStream, (uint)TargetEntityId);

        bitStream.WriteRanged(Math.Min(Amount, AmountMax), AmountMax);
        bitStream.WriteRanged(Math.Min(Kind, KindMax), KindMax);

        InventoryItemRef.WriteUInt32(bitStream, (uint)Unnamed);

        if (Result is null)
        {
            bitStream.Write0();
        }
        else
        {
            bitStream.Write1();
            bitStream.WriteRanged(Math.Min(Result.Value, ResultMax), ResultMax);
        }

        return bitStream;
    }
}
