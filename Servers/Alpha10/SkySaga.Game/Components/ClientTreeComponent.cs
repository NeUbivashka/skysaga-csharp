namespace SkySaga.Game.Components;

public readonly record struct TreePart(
    byte PartType,
    byte DirectionFromParent,
    int ParentIndex,
    byte FacingDirection,
    bool IsDecoration,
    byte OriginalVoxelType);

public class ClientTreeComponent : Component
{
    private const uint PartCountEscape = 0x7F;
    private const uint PartTypeMax = 0x38;
    private const uint DirectionMax = 7;
    private const uint ParentIndexMax = 0x7F;

    public bool ClimbFromAnyAngle { get; set => SetIfChanged(ref field, value); }
    public uint? DecorationLootTable { get; set => SetIfChanged(ref field, value); }
    public List<TreePart> Description { get; set => SetIfChanged(ref field, value); } = [];
    public bool HighlightDuringFTUE { get; set => SetIfChanged(ref field, value); }
    public bool IsClimbable { get; set => SetIfChanged(ref field, value); }
    public List<bool> PartsDestroyed { get; set => SetIfChanged(ref field, value); } = [];
    public uint? TreeType { get; set => SetIfChanged(ref field, value); }
    public uint? TrunkLootTable { get; set => SetIfChanged(ref field, value); }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(Description), StringComparison.OrdinalIgnoreCase))
        {
            if (Description.Count == 0)
                return false;

            bitStream.WriteEscapedCount((uint)Description.Count, PartCountEscape);

            for (int index = 0; index < Description.Count; index++)
            {
                var part = Description[index];

                if (part.PartType > PartTypeMax
                    || part.DirectionFromParent > DirectionMax
                    || part.FacingDirection > DirectionMax
                    || part.ParentIndex < -1
                    || part.ParentIndex >= index
                    || part.ParentIndex + 1 > ParentIndexMax)
                    throw new InvalidOperationException($"Tree part {index} has invalid protocol fields.");

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
            if (PartsDestroyed.Count == 0)
                return false;

            if (PartsDestroyed.Count != Description.Count)
                throw new InvalidOperationException(
                    $"Tree has {Description.Count} parts but {PartsDestroyed.Count} destruction flags.");

            bitStream.WriteEscapedCount((uint)PartsDestroyed.Count, PartCountEscape);

            foreach (var destroyed in PartsDestroyed)
                bitStream.Write(destroyed);

            return true;
        }

        if (parameterName.Equals(nameof(TreeType), StringComparison.OrdinalIgnoreCase))
        {
            if (!TreeType.HasValue)
                return false;

            bitStream.WriteOptional(TreeType, bitStream.WriteFullWidthUInt32);
            return true;
        }

        if (parameterName.Equals(nameof(DecorationLootTable), StringComparison.OrdinalIgnoreCase))
        {
            if (!DecorationLootTable.HasValue)
                return false;

            bitStream.WriteOptional(DecorationLootTable, bitStream.WriteFullWidthUInt32);
            return true;
        }

        if (parameterName.Equals(nameof(TrunkLootTable), StringComparison.OrdinalIgnoreCase))
        {
            if (!TrunkLootTable.HasValue)
                return false;

            bitStream.WriteOptional(TrunkLootTable, bitStream.WriteFullWidthUInt32);
            return true;
        }

        if (parameterName.Equals(nameof(ClimbFromAnyAngle), StringComparison.OrdinalIgnoreCase))
        {
            if (!ClimbFromAnyAngle)
                return false;

            bitStream.Write(ClimbFromAnyAngle);
            return true;
        }

        if (parameterName.Equals(nameof(HighlightDuringFTUE), StringComparison.OrdinalIgnoreCase))
        {
            if (!HighlightDuringFTUE)
                return false;

            bitStream.Write(HighlightDuringFTUE);
            return true;
        }

        if (parameterName.Equals(nameof(IsClimbable), StringComparison.OrdinalIgnoreCase))
        {
            if (!IsClimbable)
                return false;

            bitStream.Write(IsClimbable);
            return true;
        }

        return false;
    }
}
