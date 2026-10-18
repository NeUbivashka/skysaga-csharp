namespace SkySaga.Game.Packets;

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
