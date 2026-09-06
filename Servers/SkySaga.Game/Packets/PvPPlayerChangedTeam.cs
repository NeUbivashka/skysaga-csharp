namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 95, server to client. Reader: FUN_007FB870 -> FUN_00BB3E50(1, 0x5F, ...).
///
///     PlayerName   string
///     FromTeam     ranged, max 2 -> 2 bits
///     ToTeam       ranged, max 2 -> 2 bits
///
/// Two identical two-bit fields after a name is what a team change looks like; which is the old
/// side and which the new is a reading of the order, not of the code.
/// </summary>
public class PvPPlayerChangedTeam : ISerializablePacket
{
    private const uint TeamMax = 2;

    public string? PlayerName;
    public uint FromTeam;
    public uint ToTeam;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.PvPPlayerChangedTeam);

        bitStream.WriteString(PlayerName);
        bitStream.WriteRanged(Math.Min(FromTeam, TeamMax), TeamMax);
        bitStream.WriteRanged(Math.Min(ToTeam, TeamMax), TeamMax);

        return bitStream;
    }
}
