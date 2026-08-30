namespace SkySaga.Game.Packets;

public class SetInputMovement : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetInputMovement);

        return bitStream;
    }
}
