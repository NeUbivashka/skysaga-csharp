namespace SkySaga.Game.Packets;

public class InstanceOpenForMatchmakingNewPlayersState : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.InstanceOpenForMatchmakingNewPlayersState);

        return bitStream;
    }
}
