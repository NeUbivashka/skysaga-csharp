namespace SkySaga.Game.Packets;

public static class SetPlayerState
{
    public const uint StateMax = 10;

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadInt32(StateMax, out var state)) return false;

        connection.PlayerState = state;

        return true;
    }
}
