namespace SkySaga.Game.Components;

public class ClientDynamicPlaceAgainstWallComponent : Component
{
    private const uint FaceMax = 7;
    private const uint PlacementDataCount = 7;

    public byte PlacedOnFace { get; set => SetIfChanged(ref field, value); }

    public uint PlacementData { get; set => SetIfChanged(ref field, value); }

    public static byte FaceOf(Vector3 normal)
    {
        var (x, y, z) = (MathF.Round(normal.X), MathF.Round(normal.Y), MathF.Round(normal.Z));

        return (x, y, z) switch
        {
            (> 0, _, _) => 0,
            (< 0, _, _) => 1,
            (_, > 0, _) => 2,
            (_, < 0, _) => 3,
            (_, _, > 0) => 4,
            (_, _, < 0) => 5,
            _ => 2
        };
    }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(PlacedOnFace), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteRanged(Math.Min(PlacedOnFace, FaceMax), FaceMax);
            return true;
        }

        if (parameterName.Equals(nameof(PlacementData), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteRangedIndex(Math.Min(PlacementData, PlacementDataCount - 1), PlacementDataCount);
            return true;
        }

        return false;
    }
}
