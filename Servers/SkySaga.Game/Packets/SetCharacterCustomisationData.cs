using System.Linq;

namespace SkySaga.Game.Packets;

/// <summary>
/// RPCSetCharacterCustomisationData, ordinal 53, client to server only (send stub FUN_00B21A10,
/// no receive stub). Sent by the character creator (script action
/// faGame_SetCharacterCustomisationData) on every change, not only at the end.
///
/// Layout from the send stub: the 32-bit entity id (byte-swapped like every full-width field),
/// then the CustomisationData struct written by FUN_00B22A00, the mirror of the Player binding
/// reader FUN_00BFA830. 263 bits in all (client RPC stats). The struct is kept as raw bits.
/// </summary>
public static class SetCharacterCustomisationData
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        var entityIdBytes = new byte[4];
        var data = new byte[(ClientCharacterCustomisationComponent.CustomisationDataBits + 7) / 8];

        var unread = bitStream.GetNumberOfBitsUsed() - (int)bitStream.GetReadOffset();

        if (unread < 32 + ClientCharacterCustomisationComponent.CustomisationDataBits
            || !bitStream.ReadBits(entityIdBytes, 32, true)
            || !bitStream.ReadBits(data, ClientCharacterCustomisationComponent.CustomisationDataBits, true))
        {
            Console.WriteLine($"[CharacterCreation] SetCharacterCustomisationData: short payload ({unread} bits), ignored.");

            return true;
        }

        // Kept on the connection, not applied to the entity: only the one in force at
        // SaveCharacterName counts.
        connection.CustomisationPayload = data;

        Console.WriteLine("[CharacterCreation] SetCharacterCustomisationData: "
            + string.Join(" ", data.Select(b => b.ToString("x2"))));

        return true;
    }
}
