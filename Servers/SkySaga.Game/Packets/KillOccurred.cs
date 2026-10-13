namespace SkySaga.Game.Packets;

public class KillOccurred : ISerializablePacket
{
    private const uint KindMax = 9;

    public int TargetEntityId;

    public int SourceEntityId;

    public InventoryItemRef Item = new();

    public uint Kind;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.KillOccurred);

        InventoryItemRef.WriteUInt32(bitStream, (uint)SourceEntityId);
        InventoryItemRef.WriteUInt32(bitStream, (uint)TargetEntityId);

        Item.Encode(bitStream);

        bitStream.WriteRanged(Math.Min(Kind, KindMax), KindMax);

        return bitStream;
    }
}
