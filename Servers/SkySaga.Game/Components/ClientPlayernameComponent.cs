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

            return true;
        }

        // currentsigil and availableplayersigillist are not implemented. This used to return true
        // for them too, without writing a bit: their mask bits went out set with no data, and the
        // client read every later Player parameter (sync index 12 onwards) shifted - appearance,
        // islocal, ismoveable, position. Found 2026-10-12 when unlocking the Sigils feature made the
        // client walk a garbage sigil list (FTL 0x4E0F in FUN_00948D80).
        return false;
    }
}
