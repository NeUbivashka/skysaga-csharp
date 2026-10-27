namespace SkySaga.Game.Packets;

public static class SaveCharacterName
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        var name = bitStream.ReadString();

        Console.WriteLine($"[CharacterCreation] SaveCharacterName: '{name}'.");

        var player = connection.PlayerEntity;

        if (connection.CharacterId is { } characterId && !string.IsNullOrWhiteSpace(name) && PlayerSaveStore.IsNameTaken(name, characterId))
        {
            Console.WriteLine($"[CharacterCreation] The name '{name}' belongs to another character; the name is not changed.");
            name = "";
        }

        if (!string.IsNullOrWhiteSpace(name) && player.TryGetComponent<ClientPlayernameComponent>(out var playername))
            playername.Playername = name;

        if (player.TryGetComponent<ClientCharacterCustomisationComponent>(out var customisation))
        {
            if (connection.CustomisationPayload is { } data)
                customisation.CustomisationData = data;

            customisation.IsCharacterCustomised = true;
        }

        connection.Send(new CharacterCreationResponse { Result = 0 });
        connection.SaveState();

        Console.WriteLine("[CharacterCreation] Character saved, CharacterCreationResponse(0) sent.");

        return true;
    }
}
