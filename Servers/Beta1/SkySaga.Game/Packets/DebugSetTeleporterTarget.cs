namespace SkySaga.Game.Packets;

public class DebugSetTeleporterTarget : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugSetTeleporterTarget);

        return bitStream;
    }
}
