namespace SkySaga.Game.Components;

public class ClientProjectileComponent : Component
{
    private const uint GravityMax = 0x10000;
    private const int GravityOffset = 0x8000;
    private const uint AngleMax = 0x6400;
    private const int AngleOffset = 0x3200;
    private const float Scale = 32f;

    public float Gravity { get; set => SetIfChanged(ref field, value); }
    public int OwningEntityId { get; set => SetIfChanged(ref field, value); }
    public int LauncherEntityId { get; set => SetIfChanged(ref field, value); }
    public uint ProjectileDataIndex { get; set => SetIfChanged(ref field, value, "ProjectileComponentData"); }
    public uint ProjectileDataCount { get; set; }
    public float HomingProjectileMaxAngle { get; set => SetIfChanged(ref field, value); }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(Gravity), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteRanged(ToFixed(Gravity, GravityOffset, GravityMax), GravityMax);
            return true;
        }

        if (parameterName.Equals(nameof(OwningEntityId), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteFullWidthUInt32((uint)OwningEntityId);
            return true;
        }

        if (parameterName.Equals(nameof(LauncherEntityId), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteFullWidthUInt32((uint)LauncherEntityId);
            return true;
        }

        if (parameterName.Equals("ProjectileComponentData", StringComparison.OrdinalIgnoreCase) && ProjectileDataCount > 0)
        {
            bitStream.WriteRangedIndex(ProjectileDataIndex + 1, ProjectileDataCount + 1);
            return true;
        }

        if (parameterName.Equals(nameof(HomingProjectileMaxAngle), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteRanged(ToFixed(HomingProjectileMaxAngle, AngleOffset, AngleMax), AngleMax);
            return true;
        }

        return false;
    }

    private static uint ToFixed(float value, int offset, uint max)
        => (uint)Math.Clamp((int)MathF.Round(value * Scale) + offset, 0, (int)max);
}
