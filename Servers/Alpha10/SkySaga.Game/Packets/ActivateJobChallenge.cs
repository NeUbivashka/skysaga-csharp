namespace SkySaga.Game.Packets;

public static class ActivateJobChallenge
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!ReadCrc(bitStream, out var challengeCrc) || !ReadCrc(bitStream, out var fromCrc))
            return false;

        Console.WriteLine($"[Challenge] activate 0x{challengeCrc:X8} from 0x{fromCrc:X8}");

        JobChallenges.Activate(connection, challengeCrc);
        return true;
    }

    private static bool ReadCrc(BitStream bitStream, out uint crc)
    {
        crc = 0;
        return !bitStream.ReadBit() || bitStream.ReadFullWidthUInt32(out crc);
    }
}
