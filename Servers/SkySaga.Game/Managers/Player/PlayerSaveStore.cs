namespace SkySaga.Game.Managers.Player;

public static class PlayerSaveStore
{
    private static readonly string SaveDirectory = Path.Combine(AppContext.BaseDirectory, "Data", "Saves");
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
                return null;

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
