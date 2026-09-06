namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 26, server to client. Reader: FUN_007FF210 -> FUN_00BB3E50(1, 0x1A, ...).
///
///     Specs     FUN_007E7480   count via FUN_007D7380: ranged max 0x20 -> 6 bits, escape at 32
///                              (1 bit + 32 bits); then count x FUN_007E9810
///     Unnamed   32 bits, full width -> big-endian
///
/// FUN_007E9810 is the MapSpec reader - the same one MapDefinition and TeleportBegins go through.
/// So this is literally a list of map specs, which is what a rotation is.
///
/// An empty list is legal and costs 6 bits.
/// </summary>
public class SetMapRotationList : ISerializablePacket
{
    /// <summary>Above this the count carries an escape bit plus a full 32-bit count.</summary>
    private const uint SpecCountMax = 0x20;

    public List<MapSpec> Specs = [];

    public uint Unnamed;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetMapRotationList);

        var count = (uint)Specs.Count;

        bitStream.WriteRanged(Math.Min(count, SpecCountMax), SpecCountMax);

        if (count >= SpecCountMax)
        {
            bitStream.Write1();
            InventoryItemRef.WriteUInt32(bitStream, count);
        }

        foreach (var spec in Specs)
            spec.Encode(bitStream);

        InventoryItemRef.WriteUInt32(bitStream, Unnamed);

        return bitStream;
    }
}
