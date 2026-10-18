namespace SkySaga.Game.Packets;

public static class JoinDestinationSelectionWorld
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadFullWidthUInt32(out var value)) return false;

        Console.WriteLine($"[JoinDestinationSelectionWorld] triggeringEntityID={value}");

        return true;
    }
}
