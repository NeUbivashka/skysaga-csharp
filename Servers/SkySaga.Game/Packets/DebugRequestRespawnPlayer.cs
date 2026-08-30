namespace SkySaga.Game.Packets;

public class DebugRequestRespawnPlayer : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugRequestRespawnPlayer);

        return bitStream;
    }
}
