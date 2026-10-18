namespace SkySaga.Game.Packets;

public class XPGained : ISerializablePacket
{
    private static uint JobChallengeCount => GeoIndex.Count("JobChallenges");

    private static uint JobCount => GeoIndex.Count("Jobs");

    public int EntityId;

    public uint JobChallenge;

    public uint Job;

    public uint? Amount;

    public InventoryItemRef? Item;

    public uint ItemCount;

    public string? Reason;

    public uint? Bonus;

    public int Total;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.XPGained);

        InventoryItemRef.WriteUInt32(bitStream, (uint)EntityId);

        bitStream.WriteRangedIndex(JobChallenge, JobChallengeCount);
        bitStream.WriteRangedIndex(Job, JobCount);

        InventoryItemRef.WriteOptionalUInt32(bitStream, Amount);

        if (Item is null)
        {
            bitStream.Write0();
        }
        else
        {
            bitStream.Write1();
            Item.Encode(bitStream);
            InventoryItemRef.WriteUInt32(bitStream, ItemCount);
        }

        if (Reason is null)
        {
            bitStream.Write0();
        }
        else
        {
            bitStream.Write1();
            bitStream.WriteString(Reason);
        }

        InventoryItemRef.WriteOptionalUInt32(bitStream, Bonus);

        InventoryItemRef.WriteUInt32(bitStream, (uint)Total);

        return bitStream;
    }
}
