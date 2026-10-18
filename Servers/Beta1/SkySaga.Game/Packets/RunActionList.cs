namespace SkySaga.Game.Packets;

public class RunActionList : ISerializablePacket
{

    public required string ActionName;

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.RunActionList);

        bitStream.WriteString(ActionName);

        return bitStream;
    }
}
