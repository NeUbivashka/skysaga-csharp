namespace SkySaga.Game.Packets;

/// <summary>
/// RPCPlayerDodged, ordinal 280, client to server (send stub FUN_00B25580): one value ranged max 8
/// (4 bits), sent with player state 7.
/// </summary>
public static class PlayerDodged
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadInt32(8, out var value)) return false;

        Console.WriteLine($"[PlayerDodged] value={value}");

        return true;
    }
}
