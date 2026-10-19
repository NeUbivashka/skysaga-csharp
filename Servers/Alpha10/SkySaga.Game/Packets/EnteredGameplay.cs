using System.Linq;

namespace SkySaga.Game.Packets;

public static class EnteredGameplay
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        var totalBits = bitStream.GetNumberOfBitsUsed();
        var remainingBits = (uint)(totalBits - 8);
        var buffer = new byte[(remainingBits + 7) / 8];
        var readOk = remainingBits == 0 || bitStream.ReadBits(buffer, remainingBits, alignBitsToRight: true);
        var hex = string.Join(" ", buffer.Select(b => b.ToString("x2")));

        Console.WriteLine($"[EnteredGameplay] Received. readOk={readOk} raw: {hex}");

        const string videoName = "GCIntro_StoryTextVideo";

        var playVideo = new PlayFullScreenVideo
        {
            VideoIndex = PlayFullScreenVideo.GCIntroStoryTextVideo
        };

        connection.CurrentVideoFileName = videoName;

        SendIsLocal(connection);

        if (!connection.WorldManager.Definition.Spec.IsFtue)
        {
            Console.WriteLine("[EnteredGameplay] Not the FTUE - no intro video.");
            return true;
        }

        if (Environment.GetEnvironmentVariable("SKYSAGA_SKIP_INTRO") == "1")
        {
            Console.WriteLine("[EnteredGameplay] SKYSAGA_SKIP_INTRO=1 - not sending PlayFullScreenVideo.");
            return true;
        }

        var playVideoStream = playVideo.Serialize();

        connection.Send(playVideoStream);

        Console.WriteLine($"[EnteredGameplay] Sent PlayFullScreenVideo('{videoName}', index="
            + $"{playVideo.VideoIndex}, {playVideoStream.GetNumberOfBytesUsed()} bytes, "
            + $"{playVideoStream.GetNumberOfBitsUsed()} bits used).");

        return true;
    }

    private static void SendIsLocal(PlayerConnection connection)
    {
        var now = DateTime.UtcNow;
        connection.IsLocalDueUtc.Enqueue(now);
        connection.IsLocalDueUtc.Enqueue(now + TimeSpan.FromSeconds(1));
    }
}