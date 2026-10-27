using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SkySaga.Data.Entities;

namespace SkySaga.Auth.Services;

public static class AccountClaims
{
    public const string Stamp = "skysaga:stamp";

    public static Task SignInAsync(HttpContext context, Account account)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new Claim(ClaimTypes.Name, account.Username),
            new Claim(Stamp, account.SecurityStamp.ToString())
        ], CookieAuthenticationDefaults.AuthenticationScheme);

        return context.SignInAsync(new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true });
    }

    public static Guid? AccountId(ClaimsPrincipal user)
        => Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
