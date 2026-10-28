using SkySaga.Game.Database;

namespace SkySaga.Game.Packets;

public static class PhotoDelete
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        if (!bitStream.ReadClientUuid(out var photoId))
            return false;

        if (photoId is not { } id || connection.CharacterId is not { } characterId)
            return true;

        var deleted = PhotoStore.Delete(id, characterId);

        Console.WriteLine($"[Photo] {connection.CharacterName} {(deleted ? "deleted" : "tried to delete unknown or foreign")} photo {id}");
        return true;
    }
}
