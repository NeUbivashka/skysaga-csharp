namespace SkySaga.Game.Packets;

public class GroupOpenInstanceForMatchmakingNewPlayers : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.GroupOpenInstanceForMatchmakingNewPlayers);

        return bitStream;
    }
}
