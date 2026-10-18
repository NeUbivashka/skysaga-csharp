namespace SkySaga.Game.Packets;

public static class ClientInitialSyncFinished
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        Debug.WriteLine("", nameof(ClientInitialSyncFinished));

        connection.InitialEntitiySync();

        return true;
    }
}