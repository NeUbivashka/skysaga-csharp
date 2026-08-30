namespace SkySaga.Game.Packets;

public class RunMeandrosActions : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.RunMeandrosActions);

        return bitStream;
    }
}
