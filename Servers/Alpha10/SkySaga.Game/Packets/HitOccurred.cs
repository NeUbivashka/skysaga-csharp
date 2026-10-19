namespace SkySaga.Game.Packets;

public class HitOccurred : ISerializablePacket
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
        bitStream.WritePacketId(PacketId.HitOccurred);

        InventoryItemRef.WriteUInt32(bitStream, (uint)SourceEntityId);
        InventoryItemRef.WriteUInt32(bitStream, (uint)TargetEntityId);

        bitStream.WriteRanged(Math.Min(Amount, AmountMax), AmountMax);
        bitStream.WriteRanged(Math.Min(Kind, KindMax), KindMax);

        InventoryItemRef.WriteUInt32(bitStream, (uint)Unnamed);

        bitStream.Write0();

        return bitStream;
    }
}
