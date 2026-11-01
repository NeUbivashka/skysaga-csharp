namespace SkySaga.Game.Components;

public readonly record struct TreePart(byte PartType, byte DirectionFromParent, int ParentIndex, byte FacingDirection, bool IsDecoration,
    byte OriginalVoxelType);

public class ClientTreeComponent : Component
{
    private const uint PartCountEscape = 0x7F;
    private const uint PartTypeMax = 0x38;
    private const uint DirectionMax = 7;
    private const uint ParentIndexMax = 0x7F;

    public IReadOnlyList<TreePart> Description { get; set => SetIfChanged(ref field, value); } = [];
    public uint? TreeType { get; set => SetIfChanged(ref field, value); }
    public IReadOnlyList<bool> PartsDestroyed { get; set => SetIfChanged(ref field, value); } = [];
    public bool HighlightDuringFTUE { get; set => SetIfChanged(ref field, value); }
    public bool IsClimbable { get; set => SetIfChanged(ref field, value); }
    public bool ClimbFromAnyAngle { get; set => SetIfChanged(ref field, value); }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(Description), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteEscapedCount((uint)Description.Count, PartCountEscape);

            foreach (var part in Description)
            {
                bitStream.WriteRanged(part.PartType, PartTypeMax);
                bitStream.WriteRanged(part.DirectionFromParent, DirectionMax);
                bitStream.WriteRanged((uint)(part.ParentIndex + 1), ParentIndexMax);
                bitStream.WriteRanged(part.FacingDirection, DirectionMax);
                bitStream.Write(part.IsDecoration);
                bitStream.Write(part.OriginalVoxelType);
            }

            return true;
        }

        if (parameterName.Equals(nameof(PartsDestroyed), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteEscapedCount((uint)PartsDestroyed.Count, PartCountEscape);

            foreach (var destroyed in PartsDestroyed)
                bitStream.Write(destroyed);

            return true;
        }

        if (parameterName.Equals(nameof(TreeType), StringComparison.OrdinalIgnoreCase))
        {
            if (TreeType is uint type)
            {
                bitStream.Write1();
                bitStream.WriteFullWidthUInt32(type);
            }
            else
            {
                bitStream.Write0();
            }

            return true;
        }

        if (parameterName.Equals(nameof(HighlightDuringFTUE), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(HighlightDuringFTUE);
            return true;
        }

        if (parameterName.Equals(nameof(IsClimbable), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(IsClimbable);
            return true;
        }

        if (parameterName.Equals(nameof(ClimbFromAnyAngle), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(ClimbFromAnyAngle);
            return true;
        }

        return false;
    }
}
