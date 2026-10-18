namespace SkySaga.Game.Packets;

public class RequestCharacterServerTeleport : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.RequestCharacterServerTeleport);

        return bitStream;
    }
}
