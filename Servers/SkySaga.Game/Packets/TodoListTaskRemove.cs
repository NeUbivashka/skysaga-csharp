namespace SkySaga.Game.Packets;

public class TodoListTaskRemove : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.TodoListTaskRemove);

        return bitStream;
    }
}
