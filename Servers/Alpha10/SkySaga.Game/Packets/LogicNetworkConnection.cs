namespace SkySaga.Game.Packets;

public class LogicNetworkConnection
{
    private const uint PortMax = 0x10;

    public int SourceEntityId;
    public int TargetEntityId;
    public uint SourcePort;
    public int ChannelId;
    public uint TargetPort;
    public uint? Extra;

    public void Encode(BitStream bitStream)
    {
        InventoryItemRef.WriteUInt32(bitStream, (uint)SourceEntityId);
        InventoryItemRef.WriteUInt32(bitStream, (uint)TargetEntityId);

        bitStream.WriteRanged(Math.Min(SourcePort, PortMax), PortMax);

        InventoryItemRef.WriteUInt32(bitStream, (uint)ChannelId);

        bitStream.WriteRanged(Math.Min(TargetPort, PortMax), PortMax);

        InventoryItemRef.WriteOptionalUInt32(bitStream, Extra);
    }
}
