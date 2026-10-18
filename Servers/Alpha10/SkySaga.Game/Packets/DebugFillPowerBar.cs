namespace SkySaga.Game.Packets;

public class DebugFillPowerBar : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugFillPowerBar);

        return bitStream;
    }
}
