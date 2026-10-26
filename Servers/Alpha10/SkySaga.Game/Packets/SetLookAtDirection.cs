namespace SkySaga.Game.Packets;

public static class SetLookAtDirection
{
    private const uint AngleMax = 0x6400;
    private const int AngleBias = 0x3200;

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadInt32(AngleMax, out var rawPitch)) return false;
        if (!bitStream.ReadInt32(AngleMax, out var rawYaw)) return false;

        connection.LookPitch = (rawPitch - AngleBias) / 32f * 180f / MathF.PI;
        connection.LookYaw = (rawYaw - AngleBias) / 32f * 180f / MathF.PI;

        return true;
    }
}
