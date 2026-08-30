namespace SkySaga.Game.Packets;

public class EntityPerformedAbility : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.EntityPerformedAbility);

        return bitStream;
    }
}
