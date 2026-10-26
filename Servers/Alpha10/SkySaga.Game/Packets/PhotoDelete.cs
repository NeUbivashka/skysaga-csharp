using System.Net.Http;
using Microsoft.Data.Sqlite;
using SkySaga.Game.Extensions;

namespace SkySaga.Game.Packets;

public class PhotoDelete : ISerializablePacket
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(3) };

    public Guid? PhotoUuid;

    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        Guid photoUuid;

        if (bitStream.ReadClientUuid(out var clientUuid) && clientUuid.HasValue)
        {
            photoUuid = clientUuid.Value;
        }
        else
        {
            var guidBytes = new byte[16];
            if (!bitStream.Read(guidBytes, 16u))
                return false;

            var photoHex = Convert.ToHexString(guidBytes).ToLowerInvariant();
            if (!Guid.TryParse(photoHex, out photoUuid))
                return false;
        }

        var photoUuidStr = photoUuid.ToString("D").ToLowerInvariant();
        var photoUuidNoHyphen = photoUuid.ToString("N").ToLowerInvariant();
        var ownerUuidStr = connection.Identity.CharacterUuid.ToString("D").ToLowerInvariant();
        var ownerUuidNoHyphen = connection.Identity.CharacterUuid.ToString("N").ToLowerInvariant();
        Console.WriteLine($"[Photo] PhotoDelete received for {photoUuidStr} from {connection.Identity.Name} ({ownerUuidStr})");

        // 1. Delete from SQLite database and promote next photo if this was avatar
        DeleteFromDatabase(photoUuidStr, photoUuidNoHyphen, ownerUuidStr, ownerUuidNoHyphen);

        // 2. Delete file from disk
        DeleteFromDisk(photoUuidStr, photoUuidNoHyphen);

        // 3. Notify local web server via HTTP DELETE
        NotifyWebServer(photoUuidStr);

        return true;
    }

    private static void DeleteFromDatabase(string photoUuidStr, string photoUuidNoHyphen, string ownerUuidStr, string ownerUuidNoHyphen)
    {
        var dbPath = FindPhotosDbPath();
        if (dbPath == null || !File.Exists(dbPath))
        {
            Console.WriteLine($"[Photo] Warning: photos.sqlite3 not found (searched candidate directories)");
            return;
        }

        try
        {
            using var dbConn = new SqliteConnection($"Data Source={dbPath}");
            dbConn.Open();
            using var cmd = dbConn.CreateCommand();
            cmd.CommandText = @"
                DELETE FROM photos WHERE LOWER(uuid) = @uuid OR LOWER(REPLACE(uuid, '-', '')) = @uuidNoHyphen;
                DELETE FROM photo_likes WHERE LOWER(photo_uuid) = @uuid OR LOWER(REPLACE(photo_uuid, '-', '')) = @uuidNoHyphen;

                -- If owner has no avatar set among remaining photos, promote the newest one
                UPDATE photos SET is_avatar = 1 WHERE uuid = (
                    SELECT uuid FROM photos
                    WHERE (LOWER(owner_uuid) = @owner OR LOWER(REPLACE(owner_uuid, '-', '')) = @ownerNoHyphen)
                    ORDER BY created_at DESC LIMIT 1
                );
            ";
            cmd.Parameters.AddWithValue("@uuid", photoUuidStr);
            cmd.Parameters.AddWithValue("@uuidNoHyphen", photoUuidNoHyphen);
            cmd.Parameters.AddWithValue("@owner", ownerUuidStr);
            cmd.Parameters.AddWithValue("@ownerNoHyphen", ownerUuidNoHyphen);
            var rows = cmd.ExecuteNonQuery();
            Console.WriteLine($"[Photo] Deleted {rows} row(s) from {dbPath} for photo {photoUuidStr} and updated avatar");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Photo] Error deleting photo from DB: {ex.Message}");
        }
    }

    private static void DeleteFromDisk(string photoUuidStr, string photoUuidNoHyphen)
    {
        var photosDir = FindPhotosDirPath();
        if (photosDir == null || !Directory.Exists(photosDir))
            return;

        var files = new[]
        {
            Path.Combine(photosDir, $"{photoUuidStr}.jpg"),
            Path.Combine(photosDir, $"{photoUuidNoHyphen}.jpg")
        };

        foreach (var file in files)
        {
            if (File.Exists(file))
            {
                try
                {
                    File.Delete(file);
                    Console.WriteLine($"[Photo] Deleted photo file {file}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Photo] Error deleting photo file {file}: {ex.Message}");
                }
            }
        }
    }

    private static void NotifyWebServer(string photoUuidStr)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var url = $"http://127.0.0.1:5001/api/binary-storage/photos/{photoUuidStr}";
                using var response = await HttpClient.DeleteAsync(url);
            }
            catch
            {
                // Web server might be offline or running standalone; database deletion is already complete
            }
        });
    }

    private static string? FindPhotosDbPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Data", "photos.sqlite3"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data", "photos.sqlite3"),
            Path.Combine(Directory.GetCurrentDirectory(), "Data", "photos.sqlite3"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "Data", "photos.sqlite3"),
        };

        foreach (var c in candidates)
        {
            var full = Path.GetFullPath(c);
            if (File.Exists(full))
                return full;
        }

        return Path.GetFullPath(candidates[1]);
    }

    private static string? FindPhotosDirPath()
    {
        var dbPath = FindPhotosDbPath();
        if (dbPath != null)
        {
            var dir = Path.Combine(Path.GetDirectoryName(dbPath)!, "photos");
            return Path.GetFullPath(dir);
        }
        return null;
    }

    public BitStream Serialize()
    {
        var bitStream = new BitStream();

        bitStream.WritePacketId(PacketId.PhotoDelete);
        bitStream.WriteClientUuid(PhotoUuid);

        return bitStream;
    }
}
