namespace SkySaga.Game.Packets;

public class DebugCreateInteractionTriggerEntity : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugCreateInteractionTriggerEntity);

        return bitStream;
    }
}
