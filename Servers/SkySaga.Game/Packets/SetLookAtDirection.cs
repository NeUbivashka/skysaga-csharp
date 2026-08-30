namespace SkySaga.Game.Packets;

public static class SetLookAtDirection
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {

        if (!bitStream.ReadByte(4, out var lookAtMode)) return false;

        if (!Read15BitFloat(bitStream, out var pitch)) return false;
        if (!Read15BitFloat(bitStream, out var yaw)) return false;

        Debug.WriteLine($"lookAtMode: {lookAtMode}, pitch: {pitch}, yaw: {yaw}", nameof(SetLookAtDirection));

        return true;
    }

    private static bool Read15BitFloat(BitStream bitStream, out float floatValue)
    {
        if (!bitStream.ReadInt32(25600, out var tmpValue))
        {
            floatValue = 0;
            return false;
        }
        floatValue = (tmpValue - 0x3200) * 0.03125f;
        return true;
    }
}