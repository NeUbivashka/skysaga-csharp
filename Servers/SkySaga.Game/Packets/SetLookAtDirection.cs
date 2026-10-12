namespace SkySaga.Game.Packets;

/// <summary>
/// RPCSetLookAtDirection, ordinal 160, client to server (send stub FUN_0086D540): pitch then yaw,
/// each written by FUN_00859000 as ranged max 0x6400 of (value + 0x3200), 15 bits, in 1/32 degree.
/// There is no look-at mode in front of them (the old handler read 4 bits of one, so both angles
/// were misread).
/// </summary>
public static class SetLookAtDirection
{
    private const uint AngleMax = 0x6400;
    private const int AngleBias = 0x3200;

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadInt32(AngleMax, out var rawPitch)) return false;
        if (!bitStream.ReadInt32(AngleMax, out var rawYaw)) return false;

        connection.LookPitch = (rawPitch - AngleBias) / 32f;
        connection.LookYaw = (rawYaw - AngleBias) / 32f;

        return true;
    }
}
