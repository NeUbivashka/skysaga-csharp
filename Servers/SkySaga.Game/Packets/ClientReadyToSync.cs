namespace SkySaga.Game.Packets;

public static class ClientReadyToSync
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        Debug.WriteLine("", nameof(ClientReadyToSync));

        connection.InitialChunkSync();

        return true;
    }
}