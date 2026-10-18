namespace SkySaga.Game.Packets;

public static class RequestRespawn
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        Console.WriteLine($"[RequestRespawn] {bitStream.GetNumberOfUnreadBits()} payload bits");

        connection.Respawn();

        bitStream.IgnoreBits(bitStream.GetNumberOfUnreadBits());

        return true;
    }
}
