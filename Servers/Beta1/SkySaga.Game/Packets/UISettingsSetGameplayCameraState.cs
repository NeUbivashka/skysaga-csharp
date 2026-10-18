namespace SkySaga.Game.Packets;

public static class UISettingsSetGameplayCameraState
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadInt32(1, out var thirdPerson)) return false;
        if (!bitStream.ReadInt32(ClientSettingsComponent.CameraStateMax, out var state)) return false;

        if (connection.PlayerEntity.TryGetComponent<ClientSettingsComponent>(out var settings))
        {
            settings.ThirdPersonCamera = thirdPerson != 0;
            settings.CameraState = (uint)state;
        }

        Console.WriteLine($"[UISettings] camera thirdPerson={thirdPerson} state={state}");

        return true;
    }
}
