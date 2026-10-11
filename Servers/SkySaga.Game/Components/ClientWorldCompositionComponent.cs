namespace SkySaga.Game.Components;

/// <summary>
/// The WorldComposition entity's look-and-terrain component. Client bindings object vtable
/// 0x013498D8, read function FUN_00C7A7E0 (binding index from FUN_00B15040):
///
///   binding  parameter             wire
///   -------  --------------------  ------------------------------------------------------------
///      0     palettecrc            ranged index into the client's BiomePalette table
///                                  ([0x01487BCC] + 0x19DC / count at +0x19E0, 46 = 45 + unset).
///                                  Despite the name it is an index, not a CRC (FUN_007D7C90).
///      1     terraintype           3 bits, enum Sky, Sky_BonusWorld, Surface, Peak, Subterranean,
///                                  Enclosed (names at 0x01404A0C, max 6) (FUN_00BD82A0)
///      2     minwalkableyposition  8 bits holding value + 100, clamped to -100..100 (FUN_00C7A610)
/// </summary>
public class ClientWorldCompositionComponent : Component
{
    public const uint PaletteCount = 46;

    /// <summary>Wire index of BiomePalette_FTUE_The_Old_City (CRC 0xBC9FADBF), checked in a live client.</summary>
    public const uint FtuePalette = 29;

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

/// <summary>Client terrain types, in the order of the name table at 0x01404A0C.</summary>
public enum TerrainType : uint
{
    Sky,
    SkyBonusWorld,
    Surface,
    Peak,
    Subterranean,
    Enclosed,
}
