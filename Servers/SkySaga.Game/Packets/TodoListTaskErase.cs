namespace SkySaga.Game.Packets;

public class TodoListTaskErase : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.TodoListTaskErase);

        return bitStream;
    }
}
