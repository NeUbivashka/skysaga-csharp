namespace SkySaga.Game.Packets;

public static class TouchedVoxelTrackingSyncUp
{
    private const uint CountEscape = 100;

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadEscapedCount(CountEscape, out var count))
            return false;

        var touched = new List<uint>((int)count);
        var sides = new byte[1];

        for (var i = 0; i < count; i++)
        {
            if (!bitStream.ReadFullWidthUInt32(out var voxel))
                return false;

            if (!bitStream.ReadBits(sides, 8, alignBitsToRight: true))
                return false;

            touched.Add(voxel);
        }

        connection.TouchedVoxels = touched;

        return true;
    }
}
