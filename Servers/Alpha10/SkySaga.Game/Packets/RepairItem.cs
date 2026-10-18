namespace SkySaga.Game.Packets;

public class RepairItem : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.RepairItem);

        return bitStream;
    }
}
