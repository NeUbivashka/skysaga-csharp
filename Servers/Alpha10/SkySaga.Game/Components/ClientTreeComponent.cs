namespace SkySaga.Game.Components;

public class ClientTreeComponent : Component
{
    public uint TreeType { get; set => SetIfChanged(ref field, value); }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (!parameterName.Equals(nameof(TreeType), StringComparison.OrdinalIgnoreCase) || TreeType == 0)
            return false;

        bitStream.Write1();
        bitStream.WriteFullWidthUInt32(TreeType);
        return true;
    }
}
