using System.Linq;

namespace SkySaga.Game.Packets;

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

        connection.CustomisationPayload = data;

        Console.WriteLine("[CharacterCreation] SetCharacterCustomisationData: "
            + string.Join(" ", data.Select(b => b.ToString("x2"))));

        return true;
    }
}
