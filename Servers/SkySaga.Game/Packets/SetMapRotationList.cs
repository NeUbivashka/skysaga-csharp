namespace SkySaga.Game.Packets;

public class SetMapRotationList : ISerializablePacket
{
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
