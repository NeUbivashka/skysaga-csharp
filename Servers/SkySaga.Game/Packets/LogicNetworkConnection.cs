namespace SkySaga.Game.Packets;

/// <summary>
/// The compound value FUN_007E96F0 reads. Shared by AddLogicNetworkConnection (330) and
/// RemoveLogicNetworkConnection (331).
///
///     SourceEntityId   32 bits, full width -> big-endian
///     TargetEntityId   32 bits, full width -> big-endian
///     SourcePort       FUN_007D7260: ranged, max 0x10 -> 5 bits
///     ChannelId        32 bits, full width -> big-endian
///     TargetPort       FUN_007D7260: ranged, max 0x10 -> 5 bits
///     Extra            1 presence bit + 32 bits
///
/// Two entity ids each with a small port index is what a wiring connection looks like - logic
/// networks are SkySaga's in-world circuitry - but the names are a reading of the shape.
/// </summary>
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
