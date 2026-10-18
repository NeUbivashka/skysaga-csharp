namespace SkySaga.Game.Packets;

public static class UISettingsSetControlSettings
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadInt32(1, out var flag)) return false;

        if (connection.PlayerEntity.TryGetComponent<ClientSettingsComponent>(out var settings))
            settings.ControlFlag = flag != 0;

        Console.WriteLine($"[UISettings] control flag={flag}");

        return true;
    }
}
