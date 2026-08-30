namespace SkySaga.Game.Components;

public class ClientPlayernameComponent : Component
{
    public string? Playername
    {
        get;
        set => SetIfChanged(ref field, value);
    }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(Playername), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteString(Playername ?? " - ");
        }
        return true;
    }
}
