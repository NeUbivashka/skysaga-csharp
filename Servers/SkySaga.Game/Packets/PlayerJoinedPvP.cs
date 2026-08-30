namespace SkySaga.Game.Packets;

public class PlayerJoinedPvP : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PlayerJoinedPvP);

        return bitStream;
    }
}
