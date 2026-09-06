namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 91, server to client. Reader: FUN_008021B0 -> FUN_00BB3E50(1, 0x5B, ...).
///
///     EntityId          32 bits, full width -> big-endian
///     JobChallenge      FUN_007D7E90: 16-bit index, width from [0x01487BCC] + 0x1D28
///     Job               FUN_007D7E10: 16-bit index, width from [0x01487BCC] + 0x1D34
///     Amount            1 presence bit + 32 bits
///     Item              FUN_007E7F40: 1 bit, and if set an InventoryItemRef plus 32 bits
///     Reason            FUN_007E8050: 1 bit, and if set a string
///     Bonus             1 presence bit + 32 bits
///     Total             32 bits, full width -> big-endian
///
/// The two index fields are the same GeoData-driven mechanism MapSpec's searchable slots use, and
/// the alphabetical descriptor grid identifies them: rank 103 at 0x1D28 is JobChallenges (2010
/// entries, so 11 bits) and rank 104 at 0x1D34 is Jobs (5 entries, so 3 bits). An XP award tied to
/// a job challenge within a job is exactly what the name says.
///
/// The widths come from lists inside the client, so if a future build changes those counts the
/// widths move with them. Read the dwords at those two addresses from a running client if the
/// packet ever mis-parses.
/// </summary>
public class XPGained : ISerializablePacket
{
    /// <summary>GeoData JobChallenges: 2010 entries, so the index is 11 bits.</summary>
    private const uint JobChallengeCount = 2011;

    /// <summary>GeoData Jobs: 5 entries, so the index is 3 bits.</summary>
    private const uint JobCount = 6;

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
