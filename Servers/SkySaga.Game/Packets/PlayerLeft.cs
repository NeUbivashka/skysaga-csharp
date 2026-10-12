namespace SkySaga.Game.Packets;

public class PlayerLeft : ISerializablePacket
{
    public string? PlayerName;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PlayerLeft);

        bitStream.WriteString(PlayerName);

        return bitStream;
    }
}
