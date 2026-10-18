namespace SkySaga.Game.Components;

public class LandingPadComponent : Component
{
    public Vector3 PositionOffset { get; set => SetIfChanged(ref field, value); }
    public float YawOffsetDegrees { get; set => SetIfChanged(ref field, value); }
    public bool IsGenericLandingPad { get; set => SetIfChanged(ref field, value); }

    public (Vector3 Position, float YawDegrees) GetLandingPoint(Vector3 padPosition, float padYawDegrees)
    {
        var yaw = padYawDegrees * (MathF.PI / 180f);
        var (sin, cos) = MathF.SinCos(yaw);

        var offset = new Vector3(
            PositionOffset.X * cos + PositionOffset.Z * sin,
            PositionOffset.Y,
            PositionOffset.Z * cos - PositionOffset.X * sin);

        return (padPosition + offset, padYawDegrees + YawOffsetDegrees);
    }

    public override bool TrySync(string parameterName, BitStream bitStream) => false;
}
