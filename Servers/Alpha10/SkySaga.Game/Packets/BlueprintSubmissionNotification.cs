namespace SkySaga.Game.Packets;

public class BlueprintSubmissionNotification : ISerializablePacket
{
    public string? Message;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.BlueprintSubmissionNotification);

        bitStream.WriteString(Message);

        return bitStream;
    }
}
