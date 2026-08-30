namespace SkySaga.Game.Packets;

public class EndRace : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.EndRace);

        return bitStream;
    }
}
