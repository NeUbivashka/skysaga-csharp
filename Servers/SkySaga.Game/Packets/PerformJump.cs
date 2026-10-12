namespace SkySaga.Game.Packets;

/// <summary>RPCPerformJump, ordinal 295, client to server (send stub FUN_00B23E30). No payload.</summary>
public static class PerformJump
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream) => true;
}
