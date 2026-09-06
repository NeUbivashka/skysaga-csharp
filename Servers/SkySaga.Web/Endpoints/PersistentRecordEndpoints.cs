using System;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;

namespace SkySaga.Web.Endpoints;

public static class PersistentRecordEndpoints
{
    private static Guid _characterUUID = Guid.Empty;

    public static void MapPersistentRecordEndpoints(this WebApplication app)
    {
        app.MapGet("/GetGUID", () => new
        {
            result = new
            {
                GUID = _characterUUID
            }
        });

        app.MapGet("/api/persistent-record/characters/list", () =>
        {
            if (_characterUUID == Guid.Empty)
            {
                return Results.Ok(new
                {
                    Error = new
                    {
                        code = 11001,
                        message = "",
                        detail = ""
                    }
                });
            }

            return Results.Ok(new
            {
                result = new
                {
                    characters = new[]
                    {
                        new
                        {
                            uuid = _characterUUID,
                            name = "Player",
                            homeBiome = "Sky", // (string?)null, // null > character creation
                            positionInList = 0
                        }
                    }
                }
            });
        });

        // Client asks for the profanity list right after login; an empty list
        // is enough to let it continue.
        app.MapPost("/api/persistent-record/profanity/blacklist", () => new
        {
            result = Array.Empty<string>()
        });

        app.MapPost("/api/persistent-record/characters/_create", () =>
        {
            _characterUUID = Guid.NewGuid();

            return new
            {
                result = new
                {
                    characterUUID = _characterUUID
                }
            };
        });
    }
}