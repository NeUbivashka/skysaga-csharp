namespace SkySaga.Game.Packets;

public class MovementConsumesStamina : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.MovementConsumesStamina);

        return bitStream;
    }
}
