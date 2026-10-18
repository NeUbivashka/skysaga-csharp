namespace SkySaga.Game.Packets;

public class SetFullBodyAnimationData : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SetFullBodyAnimationData);

        return bitStream;
    }
}
