namespace SkySaga.Game.Packets;

public class PlayerRankedUp : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PlayerRankedUp);

        return bitStream;
    }
}
