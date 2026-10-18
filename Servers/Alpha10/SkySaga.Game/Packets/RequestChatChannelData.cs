namespace SkySaga.Game.Packets;

public static class RequestChatChannelData
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        var packet = new SendChatChannelData();
        connection.Send(packet);
        return true;
    }
}
