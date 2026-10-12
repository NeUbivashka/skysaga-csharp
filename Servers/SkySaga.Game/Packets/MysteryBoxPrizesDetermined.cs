namespace SkySaga.Game.Packets;

public class MysteryBoxPrizesDetermined : ISerializablePacket
{
    private const uint PrizeCountMax = 8;
    private const uint SeedMax = 0x3C00;

    public int BoxId;
    public List<MysteryBoxPrize> Prizes = [];
    public uint Seed;
    public int Unnamed;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.MysteryBoxPrizesDetermined);

        InventoryItemRef.WriteUInt32(bitStream, (uint)BoxId);

        var count = (uint)Prizes.Count;

        bitStream.WriteRanged(Math.Min(count, PrizeCountMax), PrizeCountMax);

        if (count >= PrizeCountMax)
        {
            bitStream.Write1();
            InventoryItemRef.WriteUInt32(bitStream, count);
        }

        foreach (var prize in Prizes)
            prize.Encode(bitStream);

        bitStream.WriteRanged(Math.Min(Seed, SeedMax), SeedMax);

        InventoryItemRef.WriteUInt32(bitStream, (uint)Unnamed);

        return bitStream;
    }
}
