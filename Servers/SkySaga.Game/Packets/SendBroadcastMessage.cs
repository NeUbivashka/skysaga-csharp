namespace SkySaga.Game.Packets;

public class SendBroadcastMessage : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.SendBroadcastMessage);

        return bitStream;
    }
}
