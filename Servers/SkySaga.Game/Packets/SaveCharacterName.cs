namespace SkySaga.Game.Packets;

/// <summary>
/// RPCSaveCharacterName, ordinal 162, client to server only (send stub FUN_008113B0). Payload: the
/// name as a string ({"name":"MiNo"} in the client's RPC log).
///
/// GameState_CharacterCreation (game state 0xE) sends it from FUN_00810C00 once
/// /api/account/name/checkName has answered "valid", then waits (state +0x84 = 2) for
/// CharacterCreationResponse. That reply is routed by FUN_007150E0 to FUN_00810710 only while the
/// game state is 0xE: result 0 continues, result 1 logs "Character Saved Unsuccessful".
///
/// The Player must also get IsCharacterCustomised = 1. Otherwise faGame_ShowCharacterCustomisation
/// reopens the creator straight away and the client loops Gameplay / CharacterCreation.
/// </summary>
public static class SaveCharacterName
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        // The ordinal is escaped (0xFF + 41); the dispatcher has already consumed it.
        var name = bitStream.ReadString();

        Console.WriteLine($"[CharacterCreation] SaveCharacterName: '{name}'.");

        var player = connection.PlayerEntity;

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
