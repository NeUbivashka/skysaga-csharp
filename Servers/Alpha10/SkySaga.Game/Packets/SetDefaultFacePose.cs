namespace SkySaga.Game.Packets;

public class SetDefaultFacePose : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetDefaultFacePose);

        return bitStream;
    }
}
