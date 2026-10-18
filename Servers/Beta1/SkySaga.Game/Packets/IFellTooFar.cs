namespace SkySaga.Game.Packets;

public class IFellTooFar : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.IFellTooFar);

        return bitStream;
    }
}
