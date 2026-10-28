using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SkySaga.Auth.Services;
using SkySaga.Data;
using SkySaga.Data.Entities;

namespace SkySaga.Auth.Api;

public static class SocialApi
{
    private const string SessionHeader = "X-RWPVT";
    private const string Route = "/api/social-graph";
    private const int DefaultPageSize = 20;

    public static void MapSocialApi(this WebApplication app)
    {
        app.MapPost($"{Route}/character/profile/batch", ProfileBatchAsync);
        app.MapPost($"{Route}/character/relationship/info", RelationshipInfoAsync);

        app.MapPut($"{Route}/character/follow", FollowAsync);
        app.MapPost($"{Route}/character/unfollow", UnfollowAsync);
        app.MapPost($"{Route}/character/block", BlockAsync);
        app.MapPost($"{Route}/character/block/remove", UnblockAsync);
        app.MapPost($"{Route}/character/unfriend", UnfriendAsync);
        app.MapPost($"{Route}/character/buildpermission/update", UpdateBuildPermissionAsync);

        app.MapPut($"{Route}/friendrequest/create", CreateFriendRequestAsync);
        app.MapPost($"{Route}/friendrequest/cancel", CancelFriendRequestAsync);
        app.MapPost($"{Route}/friendrequest/accept", AcceptFriendRequestAsync);
        app.MapPost($"{Route}/friendrequest/reject", RejectFriendRequestAsync);

        app.MapPost($"{Route}/character/friends/info", FriendsAsync);
        app.MapPost($"{Route}/character/follows/info", FollowsAsync);
        app.MapPost($"{Route}/character/followedby/info", FollowedByAsync);
        app.MapPost($"{Route}/friendrequest/incoming", IncomingAsync);
        app.MapPost($"{Route}/friendrequest/outgoing", OutgoingAsync);
        app.MapGet($"{Route}/character/{{id:guid}}/blocked", BlockedAsync);

        app.MapPost("/api/persistent-record/characters/_findWithOnlineInfo", FindWithOnlineInfoAsync);
    }

    private static async Task<IResult> ProfileBatchAsync(HttpRequest request, IDbContextFactory<SkySagaDbContext> factory)
    {
        var body = await BodyAsync(request);
        var recipients = Uuids(body, "recipients");

        await using var db = await factory.CreateDbContextAsync();

        var characters = await db.Characters.AsNoTracking()
            .Where(c => recipients.Contains(c.Id))
            .Select(c => new
            {
                c.Id,
                c.Name,
                AccountCreated = c.Account!.CreatedAt,
                Follows = db.CharacterFollows.Count(f => f.FollowerId == c.Id),
                FollowedBy = db.CharacterFollows.Count(f => f.FollowedId == c.Id),
                Friends = db.Friendships.Count(f => f.CharacterId == c.Id)
            })
            .ToListAsync();

        var result = characters.Select(c => new
        {
            characterOnlineData = new { characterUuid = c.Id.ToString(), characterName = c.Name },
            counts = new { follows = c.Follows, followedBy = c.FollowedBy, friends = c.Friends },
            accountCreated = c.AccountCreated.ToUnixTimeSeconds()
        });

        return Results.Json(new { result });
    }

    private static async Task<IResult> RelationshipInfoAsync(HttpRequest request, IDbContextFactory<SkySagaDbContext> factory)
    {
        var body = await BodyAsync(request);
        var requester = Uuids(body, "character").FirstOrDefault();
        var others = Uuids(body, "relationships");

        await using var db = await factory.CreateDbContextAsync();

        var openIslands = await db.Worlds.AsNoTracking()
            .Where(w => w.Kind == WorldKind.Island && w.BuildAccess == WorldAccess.Everyone && w.OwnerCharacterId != null)
            .Select(w => w.OwnerCharacterId!.Value)
            .ToListAsync();

        var known = await db.Characters.AsNoTracking().Where(c => others.Contains(c.Id)).Select(c => c.Id).ToListAsync();

        var follows = await db.CharacterFollows.AsNoTracking()
            .Where(f => (f.FollowerId == requester && known.Contains(f.FollowedId)) || (f.FollowedId == requester && known.Contains(f.FollowerId)))
            .ToListAsync();
        var blocks = await db.CharacterBlocks.AsNoTracking()
            .Where(b => (b.BlockerId == requester && known.Contains(b.BlockedId)) || (b.BlockedId == requester && known.Contains(b.BlockerId)))
            .ToListAsync();
        var requests = await db.FriendRequests.AsNoTracking()
            .Where(r => (r.RequesterId == requester && known.Contains(r.RecipientId)) || (r.RecipientId == requester && known.Contains(r.RequesterId)))
            .ToListAsync();
        var grants = await db.BuildPermissions.AsNoTracking()
            .Where(p => (p.OwnerId == requester && known.Contains(p.GranteeId)) || (p.GranteeId == requester && known.Contains(p.OwnerId)))
            .ToListAsync();
        var friends = await db.Friendships.AsNoTracking()
            .Where(f => f.CharacterId == requester && known.Contains(f.FriendId))
            .Select(f => f.FriendId)
            .ToListAsync();

        var result = known.Select(other => new Dictionary<string, object>
        {
            ["character"] = other.ToString(),
            ["blocked"] = blocks.Any(b => b.BlockerId == requester && b.BlockedId == other),
            ["blockedBy"] = blocks.Any(b => b.BlockerId == other && b.BlockedId == requester),
            ["follows"] = follows.Any(f => f.FollowerId == requester && f.FollowedId == other),
            ["followedBy"] = follows.Any(f => f.FollowerId == other && f.FollowedId == requester),
            ["requesterCanBuildOnPlayersWorld"] = openIslands.Contains(other) || grants.Any(p => p.OwnerId == other && p.GranteeId == requester),
            ["playerCanBuildOnRequesterWorld"] = openIslands.Contains(requester) || grants.Any(p => p.OwnerId == requester && p.GranteeId == other),
            ["Friend"] = friends.Contains(other),
            ["incomingFriendRequest"] = requests.Any(r => r.RequesterId == requester && r.RecipientId == other),
            ["outgoingFriendRequest"] = requests.Any(r => r.RequesterId == other && r.RecipientId == requester)
        });

        return Results.Json(new { result });
    }

    private static Task<IResult> FollowAsync(HttpRequest request, AccountService accounts, IDbContextFactory<SkySagaDbContext> factory)
        => ActAsync(request, accounts, factory, "follow", async (db, me, other, _) =>
        {
            if (await BlockedEitherWayAsync(db, me, other))
                return Refuse(StatusCodes.Status403Forbidden, "Blocked");

            if (!await db.CharacterFollows.AnyAsync(f => f.FollowerId == me && f.FollowedId == other))
                db.CharacterFollows.Add(new CharacterFollow { FollowerId = me, FollowedId = other, CreatedAt = DateTimeOffset.UtcNow });

            return null;
        });

    private static Task<IResult> UnfollowAsync(HttpRequest request, AccountService accounts, IDbContextFactory<SkySagaDbContext> factory)
        => ActAsync(request, accounts, factory, "unfollow", async (db, me, other, _) =>
        {
            await db.CharacterFollows.Where(f => f.FollowerId == me && f.FollowedId == other).ExecuteDeleteAsync();
            return null;
        });

    private static Task<IResult> BlockAsync(HttpRequest request, AccountService accounts, IDbContextFactory<SkySagaDbContext> factory)
        => ActAsync(request, accounts, factory, "block", async (db, me, other, _) =>
        {
            await db.CharacterFollows.Where(f => (f.FollowerId == me && f.FollowedId == other) || (f.FollowerId == other && f.FollowedId == me)).ExecuteDeleteAsync();
            await db.FriendRequests.Where(r => (r.RequesterId == me && r.RecipientId == other) || (r.RequesterId == other && r.RecipientId == me)).ExecuteDeleteAsync();
            await db.Friendships.Where(f => (f.CharacterId == me && f.FriendId == other) || (f.CharacterId == other && f.FriendId == me)).ExecuteDeleteAsync();

            if (!await db.CharacterBlocks.AnyAsync(b => b.BlockerId == me && b.BlockedId == other))
                db.CharacterBlocks.Add(new CharacterBlock { BlockerId = me, BlockedId = other, CreatedAt = DateTimeOffset.UtcNow });

            return null;
        });

    private static Task<IResult> UnblockAsync(HttpRequest request, AccountService accounts, IDbContextFactory<SkySagaDbContext> factory)
        => ActAsync(request, accounts, factory, "unblock", async (db, me, other, _) =>
        {
            await db.CharacterBlocks.Where(b => b.BlockerId == me && b.BlockedId == other).ExecuteDeleteAsync();
            return null;
        });

    private static Task<IResult> UnfriendAsync(HttpRequest request, AccountService accounts, IDbContextFactory<SkySagaDbContext> factory)
        => ActAsync(request, accounts, factory, "unfriend", async (db, me, other, _) =>
        {
            await db.Friendships.Where(f => (f.CharacterId == me && f.FriendId == other) || (f.CharacterId == other && f.FriendId == me)).ExecuteDeleteAsync();
            return null;
        });

    private static Task<IResult> CreateFriendRequestAsync(HttpRequest request, AccountService accounts, IDbContextFactory<SkySagaDbContext> factory)
        => ActAsync(request, accounts, factory, "friend request", async (db, me, other, body) =>
        {
            var message = Text(body, "message") ?? "";
            message = message.Length > 256 ? message[..256] : message;

            if (await BlockedEitherWayAsync(db, me, other))
                return Refuse(StatusCodes.Status403Forbidden, "Blocked");

            if (await db.Friendships.AnyAsync(f => f.CharacterId == me && f.FriendId == other))
                return null;

            if (await db.FriendRequests.AnyAsync(r => r.RequesterId == other && r.RecipientId == me))
            {
                await MakeFriendsAsync(db, me, other);
                return null;
            }

            if (!await db.FriendRequests.AnyAsync(r => r.RequesterId == me && r.RecipientId == other))
                db.FriendRequests.Add(new FriendRequest { RequesterId = me, RecipientId = other, Message = message, CreatedAt = DateTimeOffset.UtcNow });

            return null;
        });

    private static Task<IResult> CancelFriendRequestAsync(HttpRequest request, AccountService accounts, IDbContextFactory<SkySagaDbContext> factory)
        => ActAsync(request, accounts, factory, "cancel friend request", async (db, me, other, _) =>
        {
            await db.FriendRequests.Where(r => r.RequesterId == me && r.RecipientId == other).ExecuteDeleteAsync();
            return null;
        });

    private static Task<IResult> AcceptFriendRequestAsync(HttpRequest request, AccountService accounts, IDbContextFactory<SkySagaDbContext> factory)
        => ActAsync(request, accounts, factory, "accept friend request", async (db, me, other, _) =>
        {
            if (!await db.FriendRequests.AnyAsync(r => r.RequesterId == other && r.RecipientId == me))
                return Refuse(StatusCodes.Status404NotFound, "Friend request not found");

            await MakeFriendsAsync(db, me, other);
            return null;
        });

    private static Task<IResult> RejectFriendRequestAsync(HttpRequest request, AccountService accounts, IDbContextFactory<SkySagaDbContext> factory)
        => ActAsync(request, accounts, factory, "reject friend request", async (db, me, other, _) =>
        {
            await db.FriendRequests.Where(r => r.RequesterId == other && r.RecipientId == me).ExecuteDeleteAsync();
            return null;
        });

    private static Task<IResult> FriendsAsync(HttpRequest request, IDbContextFactory<SkySagaDbContext> factory)
        => ListAsync(request, factory, "characterOnlineData", "total",
            (db, me) => db.Friendships.Where(f => f.CharacterId == me).Select(f => f.FriendId));

    private static Task<IResult> FollowsAsync(HttpRequest request, IDbContextFactory<SkySagaDbContext> factory)
        => ListAsync(request, factory, "characterOnlineData", "total",
            (db, me) => db.CharacterFollows.Where(f => f.FollowerId == me).Select(f => f.FollowedId));

    private static Task<IResult> FollowedByAsync(HttpRequest request, IDbContextFactory<SkySagaDbContext> factory)
        => ListAsync(request, factory, "characterOnlineData", "total",
            (db, me) => db.CharacterFollows.Where(f => f.FollowedId == me).Select(f => f.FollowerId));

    private static Task<IResult> IncomingAsync(HttpRequest request, IDbContextFactory<SkySagaDbContext> factory)
        => ListAsync(request, factory, "results", "totalHits",
            (db, me) => db.FriendRequests.Where(r => r.RecipientId == me).Select(r => r.RequesterId));

    private static Task<IResult> OutgoingAsync(HttpRequest request, IDbContextFactory<SkySagaDbContext> factory)
        => ListAsync(request, factory, "results", "totalHits",
            (db, me) => db.FriendRequests.Where(r => r.RequesterId == me).Select(r => r.RecipientId));

    private static async Task<IResult> BlockedAsync(Guid id, IDbContextFactory<SkySagaDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();

        var result = await db.CharacterBlocks.AsNoTracking()
            .Where(b => b.BlockerId == id)
            .Join(db.Characters, b => b.BlockedId, c => c.Id, (b, c) => new { c.Id, c.Name })
            .OrderBy(c => c.Name)
            .ToListAsync();

        return Results.Json(new { result = result.Select(c => Entry(c.Id, c.Name)) });
    }

    private static async Task<IResult> FindWithOnlineInfoAsync(HttpRequest request, IDbContextFactory<SkySagaDbContext> factory)
    {
        var body = await BodyAsync(request);
        var prefix = Text(body, "nameStartsWith") ?? "";
        var startFrom = Math.Max(0, Number(body, "startFrom") ?? 0);
        var pageSize = Math.Clamp(Number(body, "pageSize") ?? DefaultPageSize, 1, 100);

        await using var db = await factory.CreateDbContextAsync();

        var query = db.Characters.AsNoTracking().Where(c => c.Name.ToLower().StartsWith(prefix.ToLower()));
        var count = await query.CountAsync();
        var page = await query.OrderBy(c => c.Name).Skip(startFrom).Take(pageSize).Select(c => new { c.Id, c.Name }).ToListAsync();

        Console.WriteLine($"[Social] find '{prefix}' -> {count}");

        return Results.Json(new { result = new Dictionary<string, object> { ["characters"] = page.Select(c => Entry(c.Id, c.Name)), ["count"] = count } });
    }

    private static async Task<IResult> UpdateBuildPermissionAsync(HttpRequest request, AccountService accounts, IDbContextFactory<SkySagaDbContext> factory)
    {
        var body = await BodyAsync(request);

        if (await RequesterAsync(request, accounts) is not { } me)
            return Refuse(StatusCodes.Status401Unauthorized, "No game session");

        if (Uuids(body, "character").FirstOrDefault() != me)
            return Refuse(StatusCodes.Status403Forbidden, "Not your character");

        var add = Uuids(body, "addBuildPermission").Where(id => id != me).ToList();
        var remove = Uuids(body, "removeBuildPermission");

        await using var db = await factory.CreateDbContextAsync();

        await db.BuildPermissions.Where(p => p.OwnerId == me && remove.Contains(p.GranteeId)).ExecuteDeleteAsync();

        var existing = await db.BuildPermissions.Where(p => p.OwnerId == me && add.Contains(p.GranteeId)).Select(p => p.GranteeId).ToListAsync();
        var known = await db.Characters.Where(c => add.Contains(c.Id)).Select(c => c.Id).ToListAsync();

        foreach (var grantee in known.Except(existing))
            db.BuildPermissions.Add(new BuildPermission { OwnerId = me, GranteeId = grantee, CreatedAt = DateTimeOffset.UtcNow });

        await db.SaveChangesAsync();
        Console.WriteLine($"[Social] build permission of {me}: +{string.Join(",", known)} -{string.Join(",", remove)}");

        return Results.Json(new { result = new { } });
    }

    private static async Task<IResult> ActAsync(HttpRequest request, AccountService accounts, IDbContextFactory<SkySagaDbContext> factory, string action,
        Func<SkySagaDbContext, Guid, Guid, JsonElement, Task<IResult?>> apply)
    {
        var body = await BodyAsync(request);
        var requester = Uuids(body, "requester").FirstOrDefault();
        var recipient = Uuids(body, "recipient").FirstOrDefault();

        if (await RequesterAsync(request, accounts) is not { } me)
            return Refuse(StatusCodes.Status401Unauthorized, "No game session");

        var other = requester == me ? recipient : recipient == me ? requester : Guid.Empty;

        if (other == Guid.Empty || other == me)
            return Refuse(StatusCodes.Status400BadRequest, "Invalid characters");

        await using var db = await factory.CreateDbContextAsync();

        if (!await db.Characters.AnyAsync(c => c.Id == other))
            return Refuse(StatusCodes.Status404NotFound, "Character not found");

        if (await apply(db, me, other, body) is { } refused)
        {
            Console.WriteLine($"[Social] {action} {me} -> {other} refused");
            return refused;
        }

        await db.SaveChangesAsync();
        Console.WriteLine($"[Social] {action} {me} -> {other}");

        return Results.Json(new { result = new { } });
    }

    private static async Task<IResult> ListAsync(HttpRequest request, IDbContextFactory<SkySagaDbContext> factory, string itemsKey, string totalKey,
        Func<SkySagaDbContext, Guid, IQueryable<Guid>> select)
    {
        var body = await BodyAsync(request);
        var me = Uuids(body, "character").FirstOrDefault();
        var firstResult = Math.Max(0, Number(body, "firstResult") ?? 0);
        var pageSize = Math.Clamp(Number(body, "pageSize") ?? DefaultPageSize, 1, 100);
        var prefix = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("filter", out var filter) ? Text(filter, "nameStartsWith") ?? "" : "";

        await using var db = await factory.CreateDbContextAsync();

        var ids = select(db, me);
        var query = db.Characters.AsNoTracking().Where(c => ids.Contains(c.Id) && c.Name.ToLower().StartsWith(prefix.ToLower()));
        var total = await query.CountAsync();
        var page = await query.OrderBy(c => c.Name).Skip(firstResult).Take(pageSize).Select(c => new { c.Id, c.Name }).ToListAsync();

        return Results.Json(new { result = new Dictionary<string, object> { [itemsKey] = page.Select(c => Entry(c.Id, c.Name)), [totalKey] = total } });
    }

    private static async Task MakeFriendsAsync(SkySagaDbContext db, Guid me, Guid other)
    {
        await db.FriendRequests.Where(r => (r.RequesterId == me && r.RecipientId == other) || (r.RequesterId == other && r.RecipientId == me)).ExecuteDeleteAsync();

        var now = DateTimeOffset.UtcNow;

        if (!await db.Friendships.AnyAsync(f => f.CharacterId == me && f.FriendId == other))
            db.Friendships.Add(new Friendship { CharacterId = me, FriendId = other, CreatedAt = now });

        if (!await db.Friendships.AnyAsync(f => f.CharacterId == other && f.FriendId == me))
            db.Friendships.Add(new Friendship { CharacterId = other, FriendId = me, CreatedAt = now });

        if (!await db.CharacterFollows.AnyAsync(f => f.FollowerId == me && f.FollowedId == other))
            db.CharacterFollows.Add(new CharacterFollow { FollowerId = me, FollowedId = other, CreatedAt = now });

        if (!await db.CharacterFollows.AnyAsync(f => f.FollowerId == other && f.FollowedId == me))
            db.CharacterFollows.Add(new CharacterFollow { FollowerId = other, FollowedId = me, CreatedAt = now });
    }

    private static Task<bool> BlockedEitherWayAsync(SkySagaDbContext db, Guid me, Guid other)
        => db.CharacterBlocks.AnyAsync(b => (b.BlockerId == me && b.BlockedId == other) || (b.BlockerId == other && b.BlockedId == me));

    private static object Entry(Guid id, string name) => new { characterUuid = id.ToString(), characterName = name };

    private static IResult Refuse(int status, string message) => Results.Json(new { error = new { errorCode = status, message } }, statusCode: status);

    private static async Task<Guid?> RequesterAsync(HttpRequest request, AccountService accounts)
        => await accounts.FindSessionAsync(request.Headers[SessionHeader].ToString(), SessionKind.Game) is { } session
           && await accounts.GetCharacterAsync(session.AccountId) is { } character
            ? character.Id
            : null;

    private static async Task<JsonElement> BodyAsync(HttpRequest request)
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

    private static List<Guid> Uuids(JsonElement body, string name)
    {
        if (body.ValueKind != JsonValueKind.Object || !body.TryGetProperty(name, out var value))
            return [];

        var items = value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToList() : [value];

        return [.. items.Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => Guid.TryParse(item.GetString(), out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)];
    }

    private static string? Text(JsonElement body, string name)
        => body.ValueKind == JsonValueKind.Object && body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? Number(JsonElement body, string name)
    {
        if (body.ValueKind != JsonValueKind.Object || !body.TryGetProperty(name, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            return number;

        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number) ? number : null;
    }
}
