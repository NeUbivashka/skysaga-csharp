using System.Linq;

namespace SkySaga.Game.Packets;

public static class SetCharacterCustomisationData
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        var entityIdBytes = new byte[4];

        if (!bitStream.ReadBits(entityIdBytes, 32, true) || !CustomisationBlock.TryRead(bitStream, out var data))
        {
            Console.WriteLine("[CharacterCreation] SetCharacterCustomisationData: unreadable payload, ignored.");

            return true;
        }

        connection.CustomisationPayload = data;

        Console.WriteLine("[CharacterCreation] SetCharacterCustomisationData: "
            + string.Join(" ", data.Select(b => b.ToString("x2"))));

        return true;
    }
}
