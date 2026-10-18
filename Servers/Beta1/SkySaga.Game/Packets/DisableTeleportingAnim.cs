namespace SkySaga.Game.Packets;

public class DisableTeleportingAnim : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DisableTeleportingAnim);

        return bitStream;
    }
}
