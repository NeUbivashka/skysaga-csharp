namespace SkySaga.Game.Packets;

public class TodoListTaskAdd : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.TodoListTaskAdd);

        return bitStream;
    }
}
