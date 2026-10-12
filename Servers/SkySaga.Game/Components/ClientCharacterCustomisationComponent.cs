namespace SkySaga.Game.Components;

/// <summary>
/// The Player's appearance. Client bindings object vtable 0x01340EF0, read function FUN_00BF9D60
/// (names to indices: FUN_00A9E970):
///
///   binding  parameter              wire
///   -------  ---------------------  -----------------------------------------------------------
///      0     customisationdata      struct (FUN_00BFA830), 231 bits - kept as the raw bits the
///                                   creator sent in RPCSetCharacterCustomisationData
///      1     customisationlistcrc   presence bit + 32-bit value (FUN_007EA4B0); absent = default
///      2     scalerangecrc          same
///      3     ischaractercustomised  1 bit. 0 makes faGame_ShowCharacterCustomisation (FUN_01207B60)
///                                   open the creator - and keep reopening it after a save.
/// </summary>
public class ClientCharacterCustomisationComponent : Component
{
    /// <summary>
    /// Size of the struct in bits, constant: the client's RPC stats report 263 bits for
    /// RPCSetCharacterCustomisationData, of which 32 are the entity id.
    /// </summary>
    public const uint CustomisationDataBits = 231;

    public byte[]? CustomisationData { get; set => SetIfChanged(ref field, value); }
    public bool IsCharacterCustomised { get; set => SetIfChanged(ref field, value); }

    public override bool TrySync(string parameterName, BitStream bitStream)
    {
        if (parameterName.Equals(nameof(CustomisationData), StringComparison.OrdinalIgnoreCase))
        {
            // Nothing to send until the creator has produced a struct; the client keeps its default.
            //
            // The crash this was once gated for (FTL in FUN_00C18D00, an index past the material
            // customisation table) came from the shifted Player stream (sync index 12 sent with no
            // data), not from the struct. Synced by default since 2026-10-12.
            if (CustomisationData is null)
                return false;

            bitStream.WriteBits(CustomisationData, CustomisationDataBits, true);

            return true;
        }
        else if (parameterName.Equals("CustomisationListCRC", StringComparison.OrdinalIgnoreCase)
                 || parameterName.Equals("ScaleRangeCRC", StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write0();

            return true;
        }
        else if (parameterName.Equals(nameof(IsCharacterCustomised), StringComparison.OrdinalIgnoreCase))
        {
            bitStream.Write(IsCharacterCustomised);

            return true;
        }

        return false;
    }
}
