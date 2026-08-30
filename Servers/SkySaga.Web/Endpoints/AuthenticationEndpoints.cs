using Microsoft.AspNetCore.Builder;

namespace SkySaga.Web.Endpoints;

public static class AuthenticationEndpoints
{
    public record ApplicationLogin(string Name, string Password);

    public record SmilegateAuthLogin(string Token);

    private static int _playerCounter = 0;

    public static void MapAuthenticationEndpoints(this WebApplication app)
    {
        app.MapPost("/api/authentication/applications/names/login", (ApplicationLogin login) => new
        {
            result = new
            {
                tokenId = "tokenId",
                refreshingTokenId = "refreshingTokenId",
                timeout = 999999
            }
        });

        app.MapPost("/api/authentication/sgauth/_login", (SmilegateAuthLogin login) => new
        {
            result = new
            {
                sgUser = "",
                memberId = _playerCounter.ToString(),
                username = $"Player_{++_playerCounter}",
                token = new
                {
                    tokenId = "tokenId",
                    refreshingTokenId = "refreshingTokenId",
                    timeout = 999999
                }
            }
        });

        app.MapPost("/api/authentication/credentials/usernames/autologin", () => new
        {
            result = new
            {
                tokenId = "tokenId",
                refreshingTokenId = "refreshingTokenId",
                timeout = 999999
            }
        });
    }
}