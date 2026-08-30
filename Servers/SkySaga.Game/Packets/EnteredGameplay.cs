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

        var videoStream = new BitStream();
        videoStream.WritePacketId(PacketId.PlayFullScreenVideo);

        var crcBytes = BitConverter.GetBytes(Util.ComputeCrc32("GCIntro_StoryTextVideo"));
        Array.Reverse(crcBytes);
        videoStream.WriteBits(crcBytes, 32, true);
        
        connection.Send(videoStream);
        
        Console.WriteLine("[EnteredGameplay] Sent PlayFullScreenVideo('GCIntro_StoryTextVideo').");

        return true;
    }
}