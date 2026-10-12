namespace SkySaga.Game.Packets;

/// <summary>
/// RPCJoinDestinationSelectionWorld, ordinal 135, client to server (send stub FUN_007B85E0, caller
/// FUN_007B7550): triggeringEntityID, one full-width (big-endian) u32 (the engine log prints
/// {"triggeringEntityID":5}, the player). Sent when the player uses the airship map. Not acted on yet.
/// </summary>
public static class JoinDestinationSelectionWorld
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadFullWidthUInt32(out var value)) return false;

        Console.WriteLine($"[JoinDestinationSelectionWorld] triggeringEntityID={value}");

        return true;
    }
}
