namespace SkySaga.Game.Packets;

public class StopCharge : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.StopCharge);

        return bitStream;
    }
}
