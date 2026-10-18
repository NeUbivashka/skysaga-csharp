namespace SkySaga.Game.Components;

public readonly record struct LinkedVoxel(Vector3Int Offset, byte VoxelType);

public class ClientVoxelLinkComponent : Component
{
    public List<LinkedVoxel> Voxels { get; set => SetIfChanged(ref field, value); } = [];

    public IEnumerable<(Vector3Int Position, byte VoxelType)> GetWorldVoxels(Vector3 origin, float yawDegrees)
    {
        var originVoxel = new Vector3Int((int)MathF.Floor(origin.X), (int)MathF.Floor(origin.Y), (int)MathF.Floor(origin.Z));
        var quarterTurns = ((int)MathF.Round(yawDegrees / 90f) % 4 + 4) % 4;

        foreach (var voxel in Voxels)
        {
            var (x, z) = (voxel.Offset.X, voxel.Offset.Z);

            for (int i = 0; i < quarterTurns; i++)
                (x, z) = (z, -x);

            yield return (originVoxel + new Vector3Int(x, voxel.Offset.Y, z), voxel.VoxelType);
        }
    }

    public override bool TrySync(string parameterName, BitStream bitStream) => false;
}
