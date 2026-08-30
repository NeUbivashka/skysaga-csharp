namespace SkySaga.Game.Packets;

public class BlueprintSubmissionNotification : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.BlueprintSubmissionNotification);

        return bitStream;
    }
}
