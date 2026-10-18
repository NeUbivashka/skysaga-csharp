namespace SkySaga.Game.Packets;

public class DebugEnableMechanic : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugEnableMechanic);

        return bitStream;
    }
}
