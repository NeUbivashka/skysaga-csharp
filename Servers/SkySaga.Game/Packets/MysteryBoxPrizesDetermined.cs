namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 166, server to client. Reader: FUN_007F7B40 -> FUN_00BB3E50(1, 0xA6, ...).
///
///     BoxId     32 bits, full width -> big-endian
///     Prizes    FUN_007E7320: count ranged max 8 (escape at 8), then that many MysteryBoxPrize
///     Seed      FUN_007D6830: ranged, max 0x3C00 -> 14 bits
///     Unnamed   32 bits, full width -> big-endian
/// </summary>
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
