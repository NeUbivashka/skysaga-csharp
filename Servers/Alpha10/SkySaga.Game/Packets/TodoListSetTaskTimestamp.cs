namespace SkySaga.Game.Packets;

public class TodoListSetTaskTimestamp : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.TodoListSetTaskTimestamp);

        return bitStream;
    }
}
