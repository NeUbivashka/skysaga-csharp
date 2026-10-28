using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SkySaga.Auth.Services;
using SkySaga.Data;
using SkySaga.Data.Entities;

namespace SkySaga.Auth.Api;

public static class PhotosApi
{
    private const string Route = "/api/binary-storage/photos";
    private const string SessionHeader = "X-RWPVT";
    private const string UploadTokenHeader = "X-RWPVTP";
    private const string UploadField = "photo";
    private const int MaxImageBytes = 8 * 1024 * 1024;
    private const int DefaultPageSize = 24;

    public static void MapPhotosApi(this WebApplication app)
    {
        app.MapPost(Route + "/{id:guid}/_upload", UploadAsync);
        app.MapGet(Route + "/{id:guid}", GetMetadataAsync);
        app.MapGet(Route + "/{id:guid}/file", GetFileAsync);
        app.MapPost(Route + "/{id:guid}/access", SetAccessAsync);
        app.MapPost(Route + "/_searchByCharacterAndWorldId", SearchByCharacterAndWorldAsync);
        app.MapPost(Route + "/_searchAvatarByCharacterId", SearchAvatarAsync);
        app.MapPost(Route + "/_searchByName", EmptySearch);
        app.MapPost(Route + "/_searchRandomByWorldId", EmptySearch);
        app.MapPost(Route + "/_searchByMapSpec", EmptySearch);
        app.MapPost(Route + "/_multiLikeGet", () => Results.Json(new { result = new { likes = new { } } }));
        app.MapPost(Route + "/_whichIsCooler", () => Results.Text(""));
    }

    private static async Task<IResult> UploadAsync(Guid id, HttpRequest request, IDbContextFactory<SkySagaDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();

        if (await db.Photos.SingleOrDefaultAsync(p => p.Id == id) is not { } photo)
            return NotFound();

        if (!Guid.TryParse(request.Headers[UploadTokenHeader].ToString(), out var token) || token != photo.UploadToken)
        {
            Console.WriteLine($"[Photo] upload of {id} refused: the {UploadTokenHeader} header does not carry its upload token");
            return Results.Json(new { errorCode = 10005 }, statusCode: StatusCodes.Status401Unauthorized);
        }

        if (!request.HasFormContentType)
            return Results.BadRequest();

        var form = await request.ReadFormAsync();
        var file = form.Files.GetFile(UploadField) ?? form.Files.FirstOrDefault();

        if (file is null || file.Length == 0 || file.Length > MaxImageBytes)
            return Results.BadRequest();

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);

        photo.Image = buffer.ToArray();
        photo.Md5 = Convert.ToHexStringLower(MD5.HashData(photo.Image));
        photo.UploadedAt = DateTimeOffset.UtcNow;

        if (photo.IsAvatar)
            await db.Photos.Where(p => p.OwnerCharacterId == photo.OwnerCharacterId && p.IsAvatar && p.Id != photo.Id).ExecuteDeleteAsync();

        await db.SaveChangesAsync();

        Console.WriteLine($"[Photo] {(photo.IsAvatar ? "avatar " : "")}photo {id} uploaded, {photo.Image.Length} bytes");

        return Results.Json(new { result = await RecordAsync(db, photo, request) });
    }

    private static async Task<IResult> GetMetadataAsync(Guid id, HttpRequest request, IDbContextFactory<SkySagaDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();

        if (await db.Photos.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id && p.UploadedAt != null) is not { } photo)
            return NotFound();

        return Results.Json(new { result = new { results = new[] { await RecordAsync(db, photo, request) } } });
    }

    private static async Task<IResult> GetFileAsync(Guid id, IDbContextFactory<SkySagaDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();
        var image = await db.Photos.Where(p => p.Id == id).Select(p => p.Image).SingleOrDefaultAsync();

        return image is null ? Results.NotFound() : Results.File(image, "image/jpeg");
    }

    private static async Task<IResult> SetAccessAsync(Guid id, HttpRequest request, AccountService accounts, IDbContextFactory<SkySagaDbContext> factory)
    {
        var requester = await RequesterAsync(request, accounts);
        var parameters = await ParametersAsync(request);

        await using var db = await factory.CreateDbContextAsync();

        if (await db.Photos.SingleOrDefaultAsync(p => p.Id == id) is not { } photo)
            return NotFound();

        if (requester != photo.OwnerCharacterId)
            return Results.Json(new { errorCode = 10005 }, statusCode: StatusCodes.Status403Forbidden);

        photo.VisiblePublic = Flag(parameters, "visiblePublic") ?? photo.VisiblePublic;
        photo.VisibleFriends = Flag(parameters, "visibleFriends") ?? photo.VisibleFriends;
        await db.SaveChangesAsync();

        Console.WriteLine($"[Photo] access of {id}: public {photo.VisiblePublic}, friends {photo.VisibleFriends}");

        return Results.Json(new { result = await RecordAsync(db, photo, request) });
    }

    private static async Task<IResult> SearchByCharacterAndWorldAsync(HttpRequest request, AccountService accounts, IDbContextFactory<SkySagaDbContext> factory)
    {
        var requester = await RequesterAsync(request, accounts);
        var parameters = await ParametersAsync(request);
        var characterId = Uuid(parameters, "characterId");
        var worldId = Uuid(parameters, "worldId");
        var pageStart = Math.Max(0, Number(parameters, "pageStart") ?? 0);
        var pageSize = Math.Clamp(Number(parameters, "pageSize") ?? DefaultPageSize, 1, 100);

        await using var db = await factory.CreateDbContextAsync();

        var query = db.Photos.AsNoTracking()
            .Where(p => p.UploadedAt != null && !p.IsAvatar && p.OwnerCharacterId == characterId)
            .Where(p => worldId == null || p.WorldId == worldId)
            .Where(p => p.OwnerCharacterId == requester || p.VisiblePublic);

        var totalHits = await query.CountAsync();
        Console.WriteLine($"[Photo] search by {requester}: character {characterId}, world {worldId} -> {totalHits}");
        var page = await query.OrderByDescending(p => p.CreatedAt).Skip(pageStart).Take(pageSize).ToListAsync();
        var results = new List<object>();

        foreach (var photo in page)
            results.Add(await RecordAsync(db, photo, request));

        return Results.Json(new { result = new { totalHits, results } });
    }

    private static async Task<IResult> SearchAvatarAsync(HttpRequest request, IDbContextFactory<SkySagaDbContext> factory)
    {
        var parameters = await ParametersAsync(request);
        var characterId = Uuid(parameters, "id");

        await using var db = await factory.CreateDbContextAsync();

        var avatar = await db.Photos.AsNoTracking()
            .Where(p => p.OwnerCharacterId == characterId && p.IsAvatar && p.UploadedAt != null)
            .OrderByDescending(p => p.UploadedAt)
            .FirstOrDefaultAsync();

        var results = avatar is null ? [] : new[] { await RecordAsync(db, avatar, request) };

        return Results.Json(new { result = new { results } });
    }

    private static IResult EmptySearch() => Results.Json(new { result = new { totalHits = 0, results = Array.Empty<object>() } });

    private static IResult NotFound() => Results.Json(new { error = new { errorCode = 404, message = "Photo not found" } }, statusCode: StatusCodes.Status404NotFound);

    private static async Task<object> RecordAsync(SkySagaDbContext db, Photo photo, HttpRequest request)
    {
        var ownerName = await db.Characters.Where(c => c.Id == photo.OwnerCharacterId).Select(c => c.Name).SingleOrDefaultAsync();
        var image = new { url = $"http://{PublicHost(request)}{Route}/{photo.Id}/file", hash = photo.Md5 ?? "" };

        var game = new Dictionary<string, object?>
        {
            ["won"] = 0,
            ["lost"] = 0,
            ["thumbsTotal"] = photo.Likes,
            ["worldUUID"] = photo.WorldId?.ToString(),
            ["position"] = photo.PlayerPosition,
            ["facingDirection"] = photo.FacingDirection,
            ["avatarPhoto"] = photo.IsAvatar,
            ["tags"] = Array.Empty<string>(),
            ["metaData"] = new { cameraposition = photo.CameraPosition, cameradirection = photo.CameraDirection }
        };

        if (photo.Caption is { } caption)
            game["caption"] = caption;

        return new
        {
            uuid = photo.Id.ToString(),
            name = photo.Caption ?? "",
            thumbnail = image,
            scaled = image,
            file = image,
            access = new { visiblePublic = photo.VisiblePublic, visibleFriends = photo.VisibleFriends },
            game,
            privateData = new { owner = photo.OwnerCharacterId.ToString(), ownerName = ownerName ?? "" }
        };
    }

    private static async Task<Guid?> RequesterAsync(HttpRequest request, AccountService accounts)
        => await accounts.FindSessionAsync(request.Headers[SessionHeader].ToString(), SessionKind.Game) is { } session
           && await accounts.GetCharacterAsync(session.AccountId) is { } character
            ? character.Id
            : null;

    private static async Task<JsonElement> ParametersAsync(HttpRequest request)
    {
        try
        {
            using var body = await JsonDocument.ParseAsync(request.Body);
            var root = body.RootElement;

            return (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("params", out var parameters) ? parameters : root).Clone();
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static Guid? Uuid(JsonElement parameters, string name)
        => parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty(name, out var value)
           && value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var parsed)
            ? parsed
            : null;

    private static int? Number(JsonElement parameters, string name)
        => parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty(name, out var value)
           && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : null;

    private static bool? Flag(JsonElement parameters, string name)
        => parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) ? parsed : null,
                _ => null
            }
            : null;

    private static string PublicHost(HttpRequest request)
    {
        var forwarded = request.Headers["X-Forwarded-Host"].ToString();
        return forwarded.Length > 0 ? forwarded : request.Host.Value ?? "127.0.0.1";
    }
}
