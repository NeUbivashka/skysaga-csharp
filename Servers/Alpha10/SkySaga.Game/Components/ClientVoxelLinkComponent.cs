namespace SkySaga.Game.Components;

public readonly record struct LinkedVoxel(Vector3Int Offset, byte VoxelType);

public class ClientVoxelLinkComponent : Component
{
    public List<LinkedVoxel> Voxels { get; set => SetIfChanged(ref field, value); } = [];

    public byte? VoxelType { get; set; }

    public IEnumerable<(Vector3Int Position, byte VoxelType)> GetWorldVoxels(Vector3 origin, float yawDegrees)
    {
        foreach (var voxel in Voxels)
            yield return (WorldCell(origin, yawDegrees, voxel.Offset), voxel.VoxelType);
    }

    public static Vector3Int WorldCell(Vector3 origin, float yawDegrees, Vector3Int offset)
    {
        var (sin, cos) = MathF.SinCos(yawDegrees * MathF.PI / 180f);
        var originVoxel = new Vector3Int(
            (int)MathF.Floor(origin.X + AnchorShift * (sin + cos)),
            (int)MathF.Floor(origin.Y),
            (int)MathF.Floor(origin.Z + AnchorShift * (cos - sin)));
        var quarterTurns = ((int)MathF.Round(yawDegrees / 90f) % 4 + 4) % 4;
        var (x, z) = (offset.X, offset.Z);

        for (int i = 0; i < quarterTurns; i++)
            (x, z) = (z, -x);

        return originVoxel + new Vector3Int(x, offset.Y, z);
    }

    private const float AnchorShift = 0.25f;
    private const uint VoxelCountEscape = 200;
    private const int OffsetMin = -15;
    private const uint OffsetRange = 30;

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (!parameterName.Equals("voxels", StringComparison.OrdinalIgnoreCase) || Voxels.Count == 0)
            return false;

        bitStream.WriteEscapedCount((uint)Voxels.Count, VoxelCountEscape);

        foreach (var voxel in Voxels)
        {
            bitStream.WriteRanged((uint)(voxel.Offset.X - OffsetMin), OffsetRange);
            bitStream.WriteRanged((uint)(voxel.Offset.Y - OffsetMin), OffsetRange);
            bitStream.WriteRanged((uint)(voxel.Offset.Z - OffsetMin), OffsetRange);
            bitStream.Write(voxel.VoxelType);
        }

        return true;
    }
}
