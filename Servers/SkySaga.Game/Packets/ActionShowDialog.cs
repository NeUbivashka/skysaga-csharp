namespace SkySaga.Game.Packets;

public class ActionShowDialog : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.ActionShowDialog);

        return bitStream;
    }
}
