namespace SkySaga.Game.Packets;

public class PlayerJoined : ISerializablePacket
{
    public string? PlayerName;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PlayerJoined);

        bitStream.WriteString(PlayerName);

        return bitStream;
    }
}
