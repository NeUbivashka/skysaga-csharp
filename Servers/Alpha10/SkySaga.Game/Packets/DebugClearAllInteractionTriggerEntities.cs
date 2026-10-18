namespace SkySaga.Game.Packets;

public class DebugClearAllInteractionTriggerEntities : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.DebugClearAllInteractionTriggerEntities);

        return bitStream;
    }
}
