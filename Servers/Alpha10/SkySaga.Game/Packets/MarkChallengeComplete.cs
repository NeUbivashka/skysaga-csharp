namespace SkySaga.Game.Packets;

public static class MarkChallengeComplete
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        uint challengeCrc = 0;

        if (bitStream.ReadBit() && !bitStream.ReadFullWidthUInt32(out challengeCrc) || bitStream.GetNumberOfUnreadBits() < 2)
            return false;

        var getReward = bitStream.ReadBit();
        var submitQuest = bitStream.ReadBit();

        Console.WriteLine($"[Challenge] mark complete 0x{challengeCrc:X8} getReward={getReward} submitQuest={submitQuest}");

        JobChallengeProgress.Submit(connection, challengeCrc);
        return true;
    }
}
