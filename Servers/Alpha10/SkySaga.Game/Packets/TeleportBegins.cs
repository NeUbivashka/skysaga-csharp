namespace SkySaga.Game.Packets;

public class TeleportBegins : ISerializablePacket
{
    public required MapSpec Spec;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.TeleportBegins);

        Spec.Encode(bitStream);

        return bitStream;
    }
}
