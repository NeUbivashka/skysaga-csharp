using System;

using Microsoft.AspNetCore.Builder;

namespace SkySaga.Web.Endpoints;

public static class GameConductorEndpoints
{
    public record GameConductorReserve(int Character, Guid ImUuid);

    // What the client is told to connect to. Defaults keep the local Windows
    // setup working; the container stack overrides them (see docker-compose.yml),
    // because 127.0.0.1 is unreachable for a client on another machine.
    private static readonly string GameIP =
        Environment.GetEnvironmentVariable("SKYSAGA_GAME_IP") ?? "127.0.0.1";

    private static readonly int WebPort =
        int.TryParse(Environment.GetEnvironmentVariable("SKYSAGA_WEB_PORT"), out var webPort) ? webPort : 5164;

    private static readonly int GamePort =
        int.TryParse(Environment.GetEnvironmentVariable("SKYSAGA_GAME_PORT"), out var gamePort) ? gamePort : 42069;

    public static void MapGameConductorEndpoints(this WebApplication app)
    {
        app.MapGet("/api/game-conductor/geonode", () => new
        {
            result = new[]
            {
                new
                {
                    uuid = Guid.NewGuid(),
                    datacentre = "UK",
                    ip = GameIP,
                    port = WebPort
                }
            }
        });

        app.MapPut("/api/game-conductor/reserve", (GameConductorReserve reserve) => new
        {
            result = new
            {
            }
        });

        app.MapPost("/api/game-conductor/retrieve", () => new
        {
            result = new
            {
                retryInMillis = 5000,
                world = Guid.NewGuid(),
                ip = GameIP,
                port = GamePort,
                server = Guid.NewGuid()
            }
        });
    }
}