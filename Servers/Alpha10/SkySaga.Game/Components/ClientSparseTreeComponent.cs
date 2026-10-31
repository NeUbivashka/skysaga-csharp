namespace SkySaga.Game.Components;

public readonly record struct TreePart(
    Vector3Int Position,
    uint SizeProfile,
    byte PartType,
    byte FacingDirection,
    bool IsDecoration,
    byte OriginalVoxelType);

public class ClientSparseTreeComponent : Component
{
    private const uint DescriptionCountEscape = 65535;
    private const uint PartsDestroyedCountEscape = 65535;
    private const uint SizeProfileMax = 29;
    private const uint PartTypeMax = 56;
    private const uint FacingDirectionMax = 7;
    private const uint LootTableCount = 640;

    private static readonly (uint DimX, uint DimZ)[] SizeProfiles =
    [
        (192, 192),
        (192, 192),
        (64, 64),
        (384, 384),
        (320, 320),
        (256, 512),
        (320, 448),
        (352, 416),
        (384, 384),
        (416, 352),
        (448, 320),
        (512, 256),
        (224, 352),
        (256, 320),
        (288, 288),
        (320, 256),
        (352, 224),
        (128, 256),
        (160, 224),
        (192, 192),
        (224, 160),
        (256, 128),
        (32, 32),
        (128, 128),
        (192, 192),
        (192, 192),
        (288, 288),
        (320, 320),
        (544, 544),
        (128, 160),
        (3200, 3200)
    ];

    public List<TreePart> Description { get; set => SetIfChanged(ref field, value); } = [];

    public uint TreeType { get; set => SetIfChanged(ref field, value); }

    public List<bool> PartsDestroyed { get; set => SetIfChanged(ref field, value); } = [];

    public ushort TrunkLootTable { get; set => SetIfChanged(ref field, value); }

    public ushort DecorationLootTable { get; set => SetIfChanged(ref field, value); }

    public bool HighlightDuringFTUE { get; set => SetIfChanged(ref field, value); }

    public bool IsClimbable { get; set => SetIfChanged(ref field, value); }

    public bool ClimbFromAnyAngle { get; set => SetIfChanged(ref field, value); }

    public bool IsVoxelOwner { get; set => SetIfChanged(ref field, value); } = true;

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        switch (parameterName.ToLowerInvariant())
        {
            case "description":
                if (Description.Count == 0)
                    return false;

                bitStream.WriteEscapedCount((uint)Description.Count, DescriptionCountEscape);

                foreach (var part in Description)
                {
                    uint dw0;
                    if (part.SizeProfile == 30)
                    {
                        dw0 = 30u << 26;
                    }
                    else
                    {
                        var (dimX, dimZ) = part.SizeProfile < (uint)SizeProfiles.Length
                            ? SizeProfiles[part.SizeProfile]
                            : (192u, 192u);
                        var packedCoords = (uint)((part.Position.Y * dimZ + part.Position.Z) * dimX + part.Position.X) & 0x03FFFFFF;
                        dw0 = (part.SizeProfile << 26) | packedCoords;
                    }

                    bitStream.WriteFullWidthUInt32(dw0);
                    bitStream.WriteRanged(part.PartType, PartTypeMax);
                    bitStream.WriteRanged(part.FacingDirection, FacingDirectionMax);
                    bitStream.Write(part.IsDecoration);
                    bitStream.Write(part.OriginalVoxelType);
                }

                return true;

            case "treetype":
                if (TreeType == 0)
                    return false;

                bitStream.Write1();
                bitStream.WriteFullWidthUInt32(TreeType);
                return true;

            case "partsdestroyed":
                var targetCount = Math.Max(PartsDestroyed.Count, Description.Count);
                if (targetCount == 0)
                    return false;

                bitStream.WriteEscapedCount((uint)targetCount, PartsDestroyedCountEscape);

                for (var i = 0; i < targetCount; i++)
                {
                    var destroyed = i < PartsDestroyed.Count && PartsDestroyed[i];
                    bitStream.Write(destroyed);
                }

                return true;

            case "trunkloottable":
                if (TrunkLootTable == 0)
                    return false;

                bitStream.WriteRangedIndex(TrunkLootTable, LootTableCount);
                return true;

            case "decorationloottable":
                if (DecorationLootTable == 0)
                    return false;

                bitStream.WriteRangedIndex(DecorationLootTable, LootTableCount);
                return true;

            case "highlightduringftue":
                bitStream.Write(HighlightDuringFTUE);
                return true;

            case "isclimbable":
                bitStream.Write(IsClimbable);
                return true;

            case "climbfromanyangle":
                bitStream.Write(ClimbFromAnyAngle);
                return true;

            case "isvoxelowner":
                bitStream.Write(IsVoxelOwner);
                return true;

            default:
                return false;
        }
    }
}
