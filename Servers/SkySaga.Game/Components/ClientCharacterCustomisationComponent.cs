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
            // Off unless SKYSAGA_SYNC_APPEARANCE=1: the saved struct crashed the client on the home
            // island (2026-10-12, FTL in FUN_00C18D00 from component post-init FUN_008475C0, an
            // index past the table at [0x01487BCC]+0x3E6C, count +0x3E70). Not understood yet.
            if (CustomisationData is null || Environment.GetEnvironmentVariable("SKYSAGA_SYNC_APPEARANCE") != "1")
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
