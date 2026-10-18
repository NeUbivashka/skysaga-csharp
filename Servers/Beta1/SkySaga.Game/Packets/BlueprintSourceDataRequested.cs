namespace SkySaga.Game.Packets;

public class BlueprintSourceDataRequested : ISerializablePacket
{
    private const uint SectionCountMax = 0x200;

    private const uint InnerCountMax = 0x8000;

    public List<Layer> Layers = [];

    public List<Prop> Props = [];

    public bool Flag;

    public class Layer
    {
        public byte Kind;
        public List<uint> Values = [];
    }

    public class Prop
    {
        public InventoryItemRef Item = new();
        public List<Placement> Placements = [];
    }

    public class Placement
    {
        public uint Position;

        public byte Rotation;

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
