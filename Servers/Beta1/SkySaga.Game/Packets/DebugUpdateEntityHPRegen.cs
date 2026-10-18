namespace SkySaga.Game.Packets;

public class DebugUpdateEntityHPRegen : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugUpdateEntityHPRegen);

        return bitStream;
    }
}
