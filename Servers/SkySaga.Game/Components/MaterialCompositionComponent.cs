namespace SkySaga.Game.Components;

public class MaterialCompositionComponent : Component
{
    private const uint MaterialCountEscape = 4;

    public uint?[] MaterialList { get; set => SetIfChanged(ref field, value); } = [];

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (!parameterName.Equals(nameof(MaterialList), StringComparison.OrdinalIgnoreCase))
            return false;

        bitStream.WriteEscapedCount((uint)MaterialList.Length, MaterialCountEscape);

        foreach (var material in MaterialList)
        {
            bitStream.Write(material.HasValue);

            if (material is { } crc)
                bitStream.WriteFullWidthUInt32(crc);
        }

        return true;
    }
}
