namespace SkySaga.Game.Packets;

public class CoreTemperatureAdjustment : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.CoreTemperatureAdjustment);

        return bitStream;
    }
}
