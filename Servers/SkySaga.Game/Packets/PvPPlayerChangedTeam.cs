namespace SkySaga.Game.Packets;

public class PvPPlayerChangedTeam : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PvPPlayerChangedTeam);

        return bitStream;
    }
}
