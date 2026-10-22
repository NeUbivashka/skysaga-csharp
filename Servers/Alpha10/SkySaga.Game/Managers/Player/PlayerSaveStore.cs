namespace SkySaga.Game.Managers.Player;

public static class PlayerSaveStore
{
    private static readonly string HomeSaveDirectory = Path.Combine(AppContext.BaseDirectory, "Data", "Saves");
    private static readonly bool IsSocialWorld = Environment.GetEnvironmentVariable("SKYSAGA_WORLD") == "social";
    private static readonly string SaveDirectory = IsSocialWorld
        ? Path.Combine(HomeSaveDirectory, "SocialHub") : HomeSaveDirectory;
    private const string DefaultFileName = "player.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public static PlayerSaveData? Load(string fileName = DefaultFileName)
    {
        try
        {
            var path = Path.Combine(SaveDirectory, fileName);

            if (!File.Exists(path))
            {
                if (!IsSocialWorld)
                    return null;

                // Seed the destination character from the home save without ever writing back to it.
                path = Path.Combine(HomeSaveDirectory, fileName);
                if (!File.Exists(path))
                    return null;
            }

            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<PlayerSaveData>(json);

            Console.WriteLine($"[PlayerSaveStore] Loaded save '{fileName}' (Playername={data?.Playername}).");

            return data;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlayerSaveStore] Failed to load save '{fileName}', starting fresh instead: {ex.Message}");
            return null;
        }
    }

    public static void Save(PlayerSaveData data, string fileName = DefaultFileName)
    {
        ArgumentNullException.ThrowIfNull(data);

        try
        {
            Directory.CreateDirectory(SaveDirectory);

            var path = Path.Combine(SaveDirectory, fileName);
            var tempPath = path + ".tmp";

            var json = JsonSerializer.Serialize(data, SerializerOptions);

            File.WriteAllText(tempPath, json);
            File.Move(tempPath, path, overwrite: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlayerSaveStore] Failed to save '{fileName}': {ex.Message}");
        }
    }
}
