namespace SkySaga.Game.Packets;

public static class PerformJump
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream) => true;
}
