namespace SkySaga.Game.Packets;

public static class AnimationSequenceNetworkDiagnostics
{
    public static bool HandleRequestStart(PlayerConnection connection, BitStream bitStream)
    {
        var name = bitStream.ReadString();
        Console.WriteLine($"[AnimationSequence RX] RequestStartAnimationSequence('{name}') from guid={connection.Guid.g:X}");
        return true;
    }

    public static bool HandleFadeComplete(PlayerConnection connection, BitStream bitStream)
    {
        Console.WriteLine($"[AnimationSequence RX] AnimationSequenceFadeComplete from guid={connection.Guid.g:X}");
        return true;
    }

    public static bool HandleProgress(PlayerConnection connection, BitStream bitStream)
    {
        var name = bitStream.ReadString();

        var raw = new byte[4];
        var ok = bitStream.ReadBits(raw, 32, alignBitsToRight: true);

        Array.Reverse(raw);
        var progress = ok ? BitConverter.ToSingle(raw, 0) : float.NaN;

        Console.WriteLine(
            $"[AnimationSequence RX] SetAnimationSequenceProgressData('{name}', {progress}) from guid={connection.Guid.g:X}");

        return true;
    }

    public static bool HandleCompleted(PlayerConnection connection, BitStream bitStream)
    {
        var name = bitStream.ReadString();
        Console.WriteLine($"[AnimationSequence RX] AnimationSequenceCompleted('{name}') from guid={connection.Guid.g:X}");
        return true;
    }

    public static bool HandleStarting(PlayerConnection connection, BitStream bitStream)
    {
        var name = bitStream.ReadString();

        var raw = new byte[4];
        var ok = bitStream.ReadBits(raw, 32, alignBitsToRight: true);

        Array.Reverse(raw);
        var duration = ok ? BitConverter.ToSingle(raw, 0) : float.NaN;

        Console.WriteLine(
            $"[AnimationSequence RX] AnimationSequenceStarting('{name}', duration={duration}) from guid={connection.Guid.g:X}");

        return true;
    }

    public static bool HandleDebugRequestStart(PlayerConnection connection, BitStream bitStream)
    {
        Console.WriteLine($"[AnimationSequence RX] DebugRequestStartAnimationSequence from guid={connection.Guid.g:X}");
        return true;
    }

    public static bool HandleDebugCatchupRequest(PlayerConnection connection, BitStream bitStream)
    {
        Console.WriteLine($"[AnimationSequence RX] DebugRequestAnimationSequenceNetworkCatchup from guid={connection.Guid.g:X}");
        return true;
    }

    public static bool HandleDebugCatchup(PlayerConnection connection, BitStream bitStream)
    {
        Console.WriteLine($"[AnimationSequence RX] DebugAnimationSequenceCatchup from guid={connection.Guid.g:X}");
        return true;
    }
}