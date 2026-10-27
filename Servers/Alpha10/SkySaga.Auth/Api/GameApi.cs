using System.Text.Json;
using SkySaga.Auth.Services;
using SkySaga.Data.Entities;

namespace SkySaga.Auth.Api;

public static class GameApi
{
    private const string TokenHeader = "X-RWPVT";
    private const string DefaultHomeBiome = "Desert";
    private const int LegalsVersion = 1;
    private const int WrongCredentials = 10005;
    private const int BannedAccount = 10052;

    public static void MapGameApi(this WebApplication app)
    {
        app.MapPost("/api/authentication/sgauth/credentials/_login", LauncherLoginAsync).RequireRateLimiting("login");
        app.MapPost("/api/authentication/sgauth/_acceptLegals", AcceptLegalsAsync);
        app.MapPost("/api/authentication/sgauth/_convertLauncherToken", ConvertLauncherTokenAsync);
        app.MapPost("/api/authentication/applications/names/login", ApplicationLogin);
        app.MapPost("/api/authentication/tokens/refresh", RefreshAsync);
        app.MapPost("/api/authentication/credentials/usernames/logout", LogoutAsync);
        app.MapPost("/api/authentication/credentials/usernames/autologin", () => Error(StatusCodes.Status401Unauthorized, WrongCredentials));

        app.MapGet("/api/persistent-record/characters/_active", ActiveCharacterAsync);
        app.MapGet("/api/persistent-record/characters/list", CharacterListAsync);
        app.MapPost("/api/persistent-record/characters/_create", CreateCharacterAsync);
        app.MapPost("/api/account/get", AccountGetAsync);
        app.MapMethods("/api/account/name/checkName", ["GET", "POST"], CheckNameAsync);
    }

    private static async Task<IResult> LauncherLoginAsync(HttpRequest request, AccountService accounts, LinkSettings links)
    {
        var body = await ReadBodyAsync(request);
        var (outcome, account) = await accounts.LoginAsync(Text(body, "username"), Text(body, "password"));

        return outcome switch
        {
            LoginOutcome.Success => Results.Json(LoginResult(await accounts.IssueAsync(account!.Id, SessionKind.Launcher), account.Username, links)),
            LoginOutcome.Banned => Error(StatusCodes.Status403Forbidden, BannedAccount),
            _ => Error(StatusCodes.Status401Unauthorized, WrongCredentials)
        };
    }

    private static async Task<IResult> AcceptLegalsAsync(HttpRequest request, AccountService accounts, LinkSettings links)
    {
        var token = request.Headers[TokenHeader].ToString();

        if (await accounts.FindSessionAsync(token, SessionKind.Launcher) is not { } session)
            return Error(StatusCodes.Status401Unauthorized, WrongCredentials);

        await accounts.RevokeAsync(token);

        return Results.Json(LoginResult(await accounts.IssueAsync(session.AccountId, SessionKind.Launcher), session.Account!.Username, links));
    }

    private static async Task<IResult> ConvertLauncherTokenAsync(HttpRequest request, AccountService accounts)
    {
        var body = await ReadBodyAsync(request);

        if (await accounts.ConvertLauncherTokenAsync(Text(body, "token")) is not { } issued)
            return ClientError(StatusCodes.Status401Unauthorized, WrongCredentials);

        return Results.Json(new { result = Token(issued) });
    }

    private static IResult ApplicationLogin()
        => Results.Json(new { result = Token(new IssuedToken(Data.SessionTokens.New(), Data.SessionTokens.New(), AccountService.TokenTimeoutSeconds)) });

    private static async Task<IResult> RefreshAsync(HttpRequest request, AccountService accounts)
    {
        var body = await ReadBodyAsync(request);
        var issued = await accounts.RefreshAsync(Text(body, "token") ?? Text(body, "refreshingTokenId"))
                     ?? await accounts.RefreshAsync(request.Headers[TokenHeader].ToString());

        return issued is null
            ? ClientError(StatusCodes.Status401Unauthorized, WrongCredentials)
            : Results.Json(new { result = Token(issued) });
    }

    private static async Task<IResult> LogoutAsync(HttpRequest request, AccountService accounts)
    {
        await accounts.RevokeAsync(request.Headers[TokenHeader].ToString());
        return Results.Json(new { result = new { } });
    }

    private static async Task<IResult> ActiveCharacterAsync(HttpRequest request, AccountService accounts)
    {
        if (await GameSessionAsync(request, accounts) is not { } session)
            return ClientError(StatusCodes.Status401Unauthorized, WrongCredentials);

        var character = await accounts.GetCharacterAsync(session.AccountId) ?? await accounts.CreateCharacterAsync(session.AccountId);
        var entry = CharacterEntry(character);

        return Results.Json(new
        {
            result = new
            {
                character = entry,
                entry.uuid,
                entry.name,
                entry.homeBiome,
                entry.positionInList
            }
        });
    }

    private static async Task<IResult> CharacterListAsync(HttpRequest request, AccountService accounts)
    {
        if (await GameSessionAsync(request, accounts) is not { } session)
            return ClientError(StatusCodes.Status401Unauthorized, WrongCredentials);

        if (await accounts.GetCharacterAsync(session.AccountId) is not { } character)
            return ClientError(StatusCodes.Status200OK, 11001);

        return Results.Json(new { result = new { characters = new[] { CharacterEntry(character) } } });
    }

    private static async Task<IResult> CreateCharacterAsync(HttpRequest request, AccountService accounts)
    {
        if (await GameSessionAsync(request, accounts) is not { } session)
            return ClientError(StatusCodes.Status401Unauthorized, WrongCredentials);

        var character = await accounts.CreateCharacterAsync(session.AccountId);

        return Results.Json(new { result = new { characterUUID = character.Id.ToString() } });
    }

    private static async Task<IResult> AccountGetAsync(HttpRequest request, AccountService accounts)
    {
        if (await GameSessionAsync(request, accounts) is not { } session)
            return ClientError(StatusCodes.Status401Unauthorized, WrongCredentials);

        var account = session.Account!;
        var name = (await accounts.GetCharacterAsync(account.Id))?.Name ?? account.Username;

        return Results.Json(new { result = new { keySubset = new { RESERVED_NAME = name } } });
    }

    private static async Task<IResult> CheckNameAsync(HttpRequest request, AccountService accounts)
    {
        var session = await GameSessionAsync(request, accounts);
        var body = await ReadBodyAsync(request);
        var name = Text(body, "name") ?? request.Query["name"].ToString();
        var allowed = AccountService.CharacterNameProblem(name) is null;
        var taken = allowed && await accounts.IsNameTakenAsync(name, session?.AccountId);
        var verdict = new Dictionary<string, bool>
        {
            ["valid"] = allowed && !taken,
            ["profane"] = false,
            ["containsNotAllowedCharacters"] = !allowed,
            ["alreadyExists"] = taken
        };

        return Results.Json(new Dictionary<string, object> { ["result"] = verdict, ["valid"] = verdict["valid"], ["profane"] = false,
            ["containsNotAllowedCharacters"] = !allowed, ["alreadyExists"] = taken });
    }

    private static Task<AuthSession?> GameSessionAsync(HttpRequest request, AccountService accounts)
        => accounts.FindSessionAsync(request.Headers[TokenHeader].ToString(), SessionKind.Game);

    private static object LoginResult(IssuedToken issued, string principal, LinkSettings links) => new
    {
        token = Token(issued),
        principal,
        legals = new
        {
            acceptedVersion = LegalsVersion,
            currentVersion = LegalsVersion,
            currentTermsAndConditionsUrl = links.Terms
        },
        accountActions = new
        {
            actionRequired = false,
            updatePassword = false,
            updateCharacterName = false,
            ticketId = ""
        }
    };

    private static object Token(IssuedToken issued) => new
    {
        tokenId = issued.TokenId,
        refreshingTokenId = issued.RefreshingTokenId,
        timeout = issued.TimeoutSeconds
    };

    private static CharacterJson CharacterEntry(Character character)
        => new(character.Id.ToString(), character.Name, DefaultHomeBiome, 0);

    private sealed record CharacterJson(string uuid, string name, string homeBiome, int positionInList);

    private static IResult Error(int status, int code) => Results.Json(new { errorCode = code }, statusCode: status);

    private static IResult ClientError(int status, int code)
        => Results.Json(new { Error = new { code, message = "", detail = "" }, errorCode = code }, statusCode: status);

    private static async Task<JsonElement?> ReadBodyAsync(HttpRequest request)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(request.Body);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement? body, string key)
    {
        if (body is not { ValueKind: JsonValueKind.Object } root)
            return null;

        if (root.TryGetProperty(key, out var direct) && direct.ValueKind == JsonValueKind.String)
            return direct.GetString();

        return root.TryGetProperty("params", out var parameters) && parameters.ValueKind == JsonValueKind.Object
               && parameters.TryGetProperty(key, out var nested) && nested.ValueKind == JsonValueKind.String
            ? nested.GetString()
            : null;
    }
}
