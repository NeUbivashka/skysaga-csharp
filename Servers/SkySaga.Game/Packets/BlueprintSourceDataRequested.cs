namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 129, server to client. Reader: FUN_007ED6C0 -> FUN_00BB3E50(1, 0x81, ...), body read by
/// FUN_007E8380.
///
///     Layers      FUN_007E6D80: count FUN_007D7400, ranged max 0x200 -> 10 bits, escape at 0x200;
///                 then that many Layer
///     Props       FUN_007E6AB0: count the same 10-bit shape; then that many Prop
///     Flag        1 bit
///
/// This was the deepest structure in the protocol - five levels down before it bottomed out - and
/// it is the actual contents of a blueprint, so the depth is earned rather than accidental.
///
/// Every list here uses a count of 10 or 16 bits with the usual escape, and the leaf record
/// (FUN_007E89E0) is three flat fields. The 88-byte prop element opens with the same
/// InventoryItemRef that KillOccurred and the mystery boxes carry, which is what finally made it
/// tractable.
///
/// Names are by role. Nothing in this packet keeps its logger strings.
/// </summary>
public class BlueprintSourceDataRequested : ISerializablePacket
{
    /// <summary>Both top-level lists: FUN_007D7400, ranged max 0x200 -> 10 bits.</summary>
    private const uint SectionCountMax = 0x200;

    /// <summary>The inner lists: FUN_007D7740, ranged max 0x8000 -> 16 bits.</summary>
    private const uint InnerCountMax = 0x8000;

    public List<Layer> Layers = [];

    public List<Prop> Props = [];

    public bool Flag;

    /// <summary>FUN_007E8AE0 - a byte and a list of 32-bit words (FUN_007E77E0).</summary>
    public class Layer
    {
        public byte Kind;
        public List<uint> Values = [];
    }

    /// <summary>FUN_007E8890 - an item reference and a list of placements (FUN_007E6CB0).</summary>
    public class Prop
    {
        public InventoryItemRef Item = new();
        public List<Placement> Placements = [];
    }

    /// <summary>FUN_007E89E0 - the leaf. Three flat fields, no nesting below.</summary>
    public class Placement
    {
        public uint Position;

        /// <summary>ranged, max 4 -> 3 bits</summary>
        public byte Rotation;

        /// <summary>ranged, max 7 -> 3 bits</summary>
        public byte Variant;
    }

    private const uint RotationMax = 4;
    private const uint VariantMax = 7;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.BlueprintSourceDataRequested);

        WriteCount(bitStream, (uint)Layers.Count, SectionCountMax);

        foreach (var layer in Layers)
        {
            bitStream.WriteBits([layer.Kind], 8, true);

            WriteCount(bitStream, (uint)layer.Values.Count, InnerCountMax);

            foreach (var value in layer.Values)
                InventoryItemRef.WriteUInt32(bitStream, value);
        }

        WriteCount(bitStream, (uint)Props.Count, SectionCountMax);

        foreach (var prop in Props)
        {
            prop.Item.Encode(bitStream);

            WriteCount(bitStream, (uint)prop.Placements.Count, InnerCountMax);

            foreach (var placement in prop.Placements)
            {
                InventoryItemRef.WriteUInt32(bitStream, placement.Position);

                bitStream.WriteRanged(Math.Min(placement.Rotation, RotationMax), RotationMax);
                bitStream.WriteRanged(Math.Min(placement.Variant, VariantMax), VariantMax);
            }
        }

        bitStream.Write(Flag);

        return bitStream;
    }

    private static void WriteCount(BitStream bitStream, uint count, uint max)
    {
        bitStream.WriteRanged(Math.Min(count, max), max);

        if (count < max)
            return;

        bitStream.Write1();
        InventoryItemRef.WriteUInt32(bitStream, count);
    }
}
