using System.Linq;
using Microsoft.EntityFrameworkCore;
using SkySaga.Data;
using SkySaga.Data.Entities;
using SkySaga.Game.Database;

namespace SkySaga.Game.Packets;

public static class SetUserToken
{
    public static bool Handle(PlayerConnection connection, BitStream bitStream)
    {
        var token = bitStream.ReadString();

        try
        {
            Identify(connection, token);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Auth] {connection.Guid}: identification failed ({exception.GetBaseException().Message})");
        }

        return true;
    }

    private static void Identify(PlayerConnection connection, string token)
    {
        using var context = GameDatabase.Context();
        var hash = SessionTokens.Hash(token);
        var now = DateTimeOffset.UtcNow;
        var session = context.AuthSessions.AsNoTracking().Include(s => s.Account)
            .SingleOrDefault(s => s.TokenHash == hash && s.Kind == SessionKind.Game && s.ExpiresAt > now);

        if (session?.Account is not { } account || account.BannedUntil > now)
        {
            Console.WriteLine($"[Auth] {connection.Guid}: the session token is unknown, expired or banned");
            return;
        }

        if (connection.AccountId is { } known && known != account.Id)
        {
            Console.WriteLine($"[Auth] {connection.Guid}: a refreshed token names another account ({account.Username}); ignored");
            return;
        }

        var character = context.Characters.AsNoTracking()
            .Where(c => c.AccountId == account.Id && c.Id == connection.ClientCharacterUuid)
            .Select(c => new { c.Id, c.Name }).SingleOrDefault();

        if (character is null)
        {
            Console.WriteLine($"[Auth] {connection.Guid}: account {account.Username} does not own character "
                + $"{connection.ClientCharacterUuid?.ToString() ?? "<no uuid>"}; sending QuitGame");
            connection.Send(new QuitGame());
            return;
        }

        connection.AccountId = account.Id;
        connection.AccountName = account.Username;

        if (connection.IsPlayerInitialized)
            return;

        connection.CharacterId = character.Id;
        connection.CharacterName = character.Name;

        Console.WriteLine($"[Auth] {connection.Guid}: account {account.Username}, character {character.Name} ({character.Id}) "
            + $"in {connection.WorldManager.Info.Kind} {connection.WorldManager.Info.Id}");
    }
}
