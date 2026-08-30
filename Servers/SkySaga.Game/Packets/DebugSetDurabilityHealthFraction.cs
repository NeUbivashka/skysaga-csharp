namespace SkySaga.Game.Packets;

public class DebugSetDurabilityHealthFraction : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugSetDurabilityHealthFraction);

        return bitStream;
    }
}
