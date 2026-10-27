using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SkySaga.Data;
using SkySaga.Data.Entities;
using SkySaga.Auth.Services;

namespace SkySaga.Auth.Api;

public static class WorldsApi
{
    private static readonly object[] IslandMapSpec =
    [
        "Forest", 0, "Home_Island_Adventure", "Easy", "", WorldCatalog.DefaultSeed, "BiomePalette_Forest_HomeIsland",
        0, 0, 0, "Home_Island", 0, "Special", "HomeIsland_6x4x6", 0, "Home_Island_World", "NoEvent"
    ];

    public static void MapWorldsApi(this WebApplication app)
    {
        app.MapGet("/api/persistent-record/worlds/listforowner/{owner:guid}", ListForOwnerAsync);
        app.MapPost("/api/persistent-record/worlds/getSpecific", GetSpecificAsync);
    }

    private static async Task<IResult> ListForOwnerAsync(Guid owner, IDbContextFactory<SkySagaDbContext> factory, GameData gameData)
    {
        await using var db = await factory.CreateDbContextAsync();

        if (!await db.Characters.AnyAsync(c => c.Id == owner))
            return Results.Json(Array.Empty<object>());

        var island = WorldCatalog.HomeIsland(db, gameData.Directory, owner);

        return Results.Json(new[] { await CardAsync(db, island) });
    }

    private static async Task<IResult> GetSpecificAsync(HttpRequest request, IDbContextFactory<SkySagaDbContext> factory)
    {
        using var body = await JsonDocument.ParseAsync(request.Body);
        var root = body.RootElement;
        var text = root.TryGetProperty("uuid", out var direct) ? direct.GetString()
            : root.TryGetProperty("params", out var parameters) && parameters.TryGetProperty("uuid", out var nested) ? nested.GetString()
            : null;

        await using var db = await factory.CreateDbContextAsync();

        var worldId = Guid.TryParse(text, out var parsed) ? parsed : Guid.Empty;
        var world = await db.Worlds.AsNoTracking().SingleOrDefaultAsync(w => w.Id == worldId)
            ?? new World { Id = worldId, Kind = WorldKind.CharacterCreation, Template = "", Seed = WorldCatalog.DefaultSeed };

        var card = await CardAsync(db, world);
        var result = new Dictionary<string, object?>(card) { ["result"] = card };

        return Results.Json(result);
    }

    private static async Task<Dictionary<string, object?>> CardAsync(SkySagaDbContext db, World world)
    {
        var ownerName = world.OwnerCharacterId is { } owner
            ? await db.Characters.Where(c => c.Id == owner).Select(c => c.Name).SingleOrDefaultAsync()
            : null;

        return new Dictionary<string, object?>
        {
            ["uuid"] = world.Id.ToString(),
            ["mapSpec"] = new object[] { IslandMapSpec.Select((value, index) => index == 5 ? world.Seed : value).ToArray(), new object[] { "", 0 }, Array.Empty<object>(), "" },
            ["owner"] = world.OwnerCharacterId?.ToString() ?? "",
            ["ownerName"] = ownerName ?? "",
            ["ownerType"] = "C",
            ["name"] = world.Name ?? world.Kind switch
            {
                WorldKind.Hub => "The City of Last Light",
                WorldKind.Island => WorldCatalog.HomeIslandName,
                _ => ""
            },
            ["homeWorld"] = world.Kind == WorldKind.Island,
            ["published"] = false
        };
    }
}
