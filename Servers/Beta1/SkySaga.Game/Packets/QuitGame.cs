namespace SkySaga.Game.Packets;

public class QuitGame : ISerializablePacket
{
    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.QuitGame);

        return bitStream;
    }

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        connection.SaveState();

        connection.Send(new QuitGame());

        Console.WriteLine("[QuitGame] Client asked to quit - state saved, QuitGame echoed back.");

        return true;
    }
}
