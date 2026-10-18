namespace SkySaga.Game.Packets;

public class PvPPlayerRequestJoinTeam : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PvPPlayerRequestJoinTeam);

        return bitStream;
    }
}
