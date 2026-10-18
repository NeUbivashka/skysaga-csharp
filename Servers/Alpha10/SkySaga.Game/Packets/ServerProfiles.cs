namespace SkySaga.Game.Packets;

public class ServerProfiles : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.ServerProfiles);

        return bitStream;
    }
}
