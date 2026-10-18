namespace SkySaga.Game.Packets;

public class DebugRequestPVPLoadout : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugRequestPVPLoadout);

        return bitStream;
    }
}
