namespace SkySaga.Game.Packets;

public class SendBroadcastMessage : ISerializablePacket
{
    public string? Message;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();
        bitStream.WritePacketId(PacketId.SendBroadcastMessage);

        bitStream.WriteString(Message);

        return bitStream;
    }
}
