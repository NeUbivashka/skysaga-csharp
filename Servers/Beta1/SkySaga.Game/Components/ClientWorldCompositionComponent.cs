namespace SkySaga.Game.Components;

public class ClientWorldCompositionComponent : Component
{
    public static uint PaletteCount => GeoIndex.Count("BiomePalettes");

    public uint PaletteCrc { get; set => SetIfChanged(ref field, value); }
    public TerrainType TerrainType { get; set => SetIfChanged(ref field, value); }
    public int MinWalkableYPosition { get; set => SetIfChanged(ref field, value); } = -100;

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(PaletteCrc), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteRangedIndex(PaletteCrc, PaletteCount);

            return true;
        }
        else if (parameterName.Equals(nameof(TerrainType), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteBits(BitConverter.GetBytes((uint)TerrainType), 32 - Util.NumBitsRequiredUInt32(6), true);

            return true;
        }
        else if (parameterName.Equals(nameof(MinWalkableYPosition), StringComparison.OrdinalIgnoreCase))
        {
            var value = (uint)(Math.Clamp(MinWalkableYPosition, -100, 100) + 100);

            bitStream.WriteBits(BitConverter.GetBytes(value), 32 - Util.NumBitsRequiredUInt32(200), true);

            return true;
        }

        return false;
    }
}

public enum TerrainType : uint
{
    Sky,
    SkyBonusWorld,
    Surface,
    Peak,
    Subterranean,
    Enclosed,
}
