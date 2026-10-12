namespace SkySaga.Game.Packets;

/// <summary>
/// RPCSetPlayerState, ordinal 51, client to server (send stub FUN_00B25D10, caller FUN_00B24BB0):
/// the local character's state byte (character state object +0x28), ranged max 10 (4 bits).
/// Sent whenever it changes; observed 0, 2, 3, 4, 5. On state 7 the client also sends
/// RPCPlayerDodged. The state names are not known yet.
/// </summary>
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
