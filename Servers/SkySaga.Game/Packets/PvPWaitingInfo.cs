namespace SkySaga.Game.Packets;

public class PvPWaitingInfo : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PvPWaitingInfo);

        return bitStream;
    }
}
