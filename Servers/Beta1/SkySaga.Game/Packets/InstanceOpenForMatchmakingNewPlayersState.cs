namespace SkySaga.Game.Packets;

public class InstanceOpenForMatchmakingNewPlayersState : ISerializablePacket
{
    public bool IsOpen;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.InstanceOpenForMatchmakingNewPlayersState);

        bitStream.Write(IsOpen);

        return bitStream;
    }
}
