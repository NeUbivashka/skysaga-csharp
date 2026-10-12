namespace SkySaga.Game.Components;

/// <summary>
/// Name and sigils of the player. Client bindings vtable 0x013470E0, read function FUN_00C533C0:
///
///   binding  parameter                 wire
///   -------  ------------------------  ----------------------------------------------------------
///      0     playername                string (FUN_007EA530)
///      1     currentsigil              ranged index into the sigil table ([0x01487BCC]+0x1EBC,
///                                      count +0x1EC0 = 27, so 5 bits); 0 = none (FUN_00C53190)
///      2     availableplayersigillist  count escaped at 0x100 (9 bits, FUN_00B47760), then per
///                                      entry (FUN_00C53760): sigil index as above + two
///                                      full-width u32 (stored +0 and +4, index at +8)
///
/// When the list changes on the local player, FUN_00948D80 walks it and indexes the sigil table
/// with each entry, so every index must be below 27.
/// </summary>
public class ClientPlayernameComponent : Component
{
    public const uint SigilTableCount = 27;
    private const uint SigilListEscape = 0x100;

    /// <summary>Index into the client's sigil table, 0 = none.</summary>
    public uint CurrentSigil { get; set => SetIfChanged(ref field, value); }

    /// <summary>Available sigils: table index and the two u32 values the client stores with each.</summary>
    public List<(uint Index, uint A, uint B)> AvailablePlayerSigilList { get; set => SetIfChanged(ref field, value); } = [];

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

        if (parameterName.Equals(nameof(CurrentSigil), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteRangedIndex(CurrentSigil, SigilTableCount);

            return true;
        }

        if (parameterName.Equals(nameof(AvailablePlayerSigilList), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.WriteEscapedCount((uint)AvailablePlayerSigilList.Count, SigilListEscape);

            foreach (var (index, a, b) in AvailablePlayerSigilList)
            {
                bitStream.WriteRangedIndex(index, SigilTableCount);
                bitStream.WriteFullWidthUInt32(a);
                bitStream.WriteFullWidthUInt32(b);
            }

            return true;
        }

        // Every other parameter belongs to another component. Returning true here without writing
        // (as this did for the sigil parameters until 2026-10-12) sets the mask bit with no data,
        // and the client reads every later Player parameter shifted.
        return false;
    }
}
