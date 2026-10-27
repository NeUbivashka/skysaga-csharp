using Microsoft.EntityFrameworkCore;
using SkySaga.Auth.Services;
using SkySaga.Data;
using SkySaga.Data.Entities;

namespace SkySaga.Auth.Api;

public static class ConductorApi
{
    private const string TokenHeader = "X-RWPVT";
    private const int GamePort = 42069;
    private const int RetryInMillis = 5000;

    public static void MapConductorApi(this WebApplication app)
    {
        app.MapPost("/api/game-conductor/retrieve", RetrieveAsync);
    }

    private static async Task<IResult> RetrieveAsync(HttpRequest request, AccountService accounts, IDbContextFactory<SkySagaDbContext> factory, GameData gameData)
    {
        if (await accounts.FindSessionAsync(request.Headers[TokenHeader].ToString(), SessionKind.Game) is not { } session
            || await accounts.GetCharacterAsync(session.AccountId) is not { } character)
            return Results.Json(new { errorCode = 10005 }, statusCode: StatusCodes.Status401Unauthorized);

        await using var db = await factory.CreateDbContextAsync();

        var world = character.CurrentWorldId
            ?? (character.IsCustomised ? WorldCatalog.Hub(db, gameData.Directory).Id : character.Id);

        var result = new
        {
            retryInMillis = RetryInMillis,
            world = world.ToString(),
            ip = PublicHost(request),
            port = GamePort,
            server = world.ToString()
        };

        return Results.Json(new { result });
    }

    private static string PublicHost(HttpRequest request)
    {
        var forwarded = request.Headers["X-Forwarded-Host"].ToString();
        var host = new HostString(forwarded.Length > 0 ? forwarded : request.Host.Value ?? "").Host;

        return host.Length > 0 ? host : "127.0.0.1";
    }
}
