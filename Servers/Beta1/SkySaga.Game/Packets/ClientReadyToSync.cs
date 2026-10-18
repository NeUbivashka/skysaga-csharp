namespace SkySaga.Game.Packets;

public static class ClientReadyToSync
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        Debug.WriteLine("", nameof(ClientReadyToSync));

        ClientConnected.SendServerInfo(connection);

        connection.InitialChunkSync();

        return true;
    }
}