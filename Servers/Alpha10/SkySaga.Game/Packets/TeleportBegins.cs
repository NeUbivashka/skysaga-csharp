namespace SkySaga.Game.Packets;

public class TeleportBegins : ISerializablePacket
{
    public MapSpec Spec = new();

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.TeleportBegins);
        Spec.Encode(bitStream);
        return bitStream;
    }
}
