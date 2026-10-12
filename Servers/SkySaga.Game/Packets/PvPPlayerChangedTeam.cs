namespace SkySaga.Game.Packets;

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
