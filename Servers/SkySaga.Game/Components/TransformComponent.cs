namespace SkySaga.Game.Components;

public class TransformComponent : Component
{
    public Vector3 Position { get; set => SetIfChanged(ref field, value); }
    public float YawDegrees { get; set => SetIfChanged(ref field, value); }
    public Vector3 Size { get; set => SetIfChanged(ref field, value); }
    public float Scale { get; set => SetIfChanged(ref field, value); }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(Position), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteBits(BitConverter.GetBytes((int)(Position.X * 64f)), 32 - Util.NumBitsRequiredUInt32(0x10000u), true);
            bitStream.WriteBits(BitConverter.GetBytes((int)(Position.Y * 64f)), 32 - Util.NumBitsRequiredUInt32(0x10000u), true);
            bitStream.WriteBits(BitConverter.GetBytes((int)(Position.Z * 64f)), 32 - Util.NumBitsRequiredUInt32(0x10000u), true);

            return true;
        }
        else if (parameterName.Equals(nameof(YawDegrees), StringComparison.OrdinalIgnoreCase))
        {
            int tmpValue = (int)(YawDegrees / 0.03125f) + 0x3200;
            bitStream.WriteBits(BitConverter.GetBytes(tmpValue), 32 - Util.NumBitsRequiredUInt32(0x6400u), true);

            return true;
        }
        else if (parameterName.Equals(nameof(Size), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteBits(BitConverter.GetBytes(Size[0]), 32 - Util.NumBitsRequiredUInt32(9), true);
            bitStream.WriteBits(BitConverter.GetBytes(Size[1]), 32 - Util.NumBitsRequiredUInt32(9), true);
            bitStream.WriteBits(BitConverter.GetBytes(Size[2]), 32 - Util.NumBitsRequiredUInt32(9), true);

            return true;
        }
        else if (parameterName.Equals(nameof(Scale), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteBits(BitConverter.GetBytes(Scale), 32 - Util.NumBitsRequiredUInt32(56), true);

            return true;
        }

        return false;
    }
}