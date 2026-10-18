namespace SkySaga.Game.Packets;

public class UpdateDailyInfo : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.UpdateDailyInfo);

        return bitStream;
    }
}
