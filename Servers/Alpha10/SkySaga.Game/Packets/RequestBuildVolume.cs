namespace SkySaga.Game.Packets;

public class RequestBuildVolume : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.RequestBuildVolume);

        return bitStream;
    }
}
