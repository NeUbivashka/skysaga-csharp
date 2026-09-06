namespace SkySaga.Game.Packets;

/// <summary>
/// Ordinal 43, server to client. Reader: FUN_007F9D80 -> FUN_00BB3E50(1, 0x2B, ...).
///
///     PlayerName   string, via FUN_007EA530
///     Kind         ranged, max 9 -> 4 bits
///
/// The PvP counterpart to PlayerJoined (41), which is the same string with no second field. The
/// four-bit value is most likely a team or side - max 9 fits either - but that is a reading of the
/// name, not of the code, so it is called Kind here.
/// </summary>
public class PlayerJoinedPvP : ISerializablePacket
{
    private const uint KindMax = 9;

    public string? PlayerName;

    public uint Kind;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PlayerJoinedPvP);

        bitStream.WriteString(PlayerName);

        bitStream.WriteRanged(Math.Min(Kind, KindMax), KindMax);

        return bitStream;
    }
}
