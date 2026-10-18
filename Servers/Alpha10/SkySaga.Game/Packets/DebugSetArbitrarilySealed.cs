namespace SkySaga.Game.Packets;

public class DebugSetArbitrarilySealed : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugSetArbitrarilySealed);

        return bitStream;
    }
}
