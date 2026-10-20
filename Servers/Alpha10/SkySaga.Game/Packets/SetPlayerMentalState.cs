namespace SkySaga.Game.Packets;

public static class SetPlayerMentalState
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadInt32(CharacterStateComponent.MentalStateMax, out var state))
            return false;

        if (state >= CharacterStateComponent.MentalStates.Length
            || !connection.PlayerEntity.TryGetComponent<ClientCharacterStateComponent>(out var characterState))
        {
            Console.WriteLine($"[CharacterState] mental state {state} rejected");
            return true;
        }

        characterState.CharacterMentalStateId = (byte)state;

        Console.WriteLine($"[CharacterState] mental state {CharacterStateComponent.MentalStates[state]}");
        return true;
    }
}
