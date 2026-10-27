using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using SkySaga.Auth.Api;
using SkySaga.Auth.Components;
using SkySaga.Auth.Services;
using SkySaga.Data;

var builder = WebApplication.CreateBuilder(args);

var gameData = Path.GetFullPath(builder.Configuration["GameDataDirectory"] ?? "../SkySaga.Game/Data", builder.Environment.ContentRootPath);
var connectionString = SkySagaDatabase.ConnectionString(gameData);
var links = new LinkSettings((builder.Configuration["PublicUrl"] ?? "http://127.0.0.1:5100").TrimEnd('/'));

builder.Services.AddDbContextFactory<SkySagaDbContext>(options => options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());
builder.Services.AddSingleton<AccountService>();
builder.Services.AddSingleton(provider => new EmailProtector(provider.GetRequiredService<IDataProtectionProvider>(), Path.Combine(gameData, "auth-keys")));
builder.Services.AddSingleton(links);
builder.Services.AddSingleton(new GameData(gameData));
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.PropertyNamingPolicy = null);

builder.WebHost.UseStaticWebAssets();

var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("SkySaga.Auth")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(gameData, "auth-keys")));

if (OperatingSystem.IsWindows())
    dataProtection.ProtectKeysWithDpapi();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "skysaga.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.Events.OnValidatePrincipal = ValidateStampAsync;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    options.KnownProxies.Add(IPAddress.Loopback);
    options.KnownProxies.Add(IPAddress.IPv6Loopback);
});

builder.Services.AddRazorComponents();

var app = builder.Build();

if (args is ["reset-code", var username])
{
    var accounts = app.Services.GetRequiredService<AccountService>();

    if (await accounts.FindAccountAsync(username) is not { } account)
    {
        Console.WriteLine($"No account named {username}.");
        return 1;
    }

    Console.WriteLine($"Password reset link for {account.Username}, valid for 24 hours:");
    Console.WriteLine(links.Reset(await accounts.CreateResetCodeAsync(account.Id)));
    return 0;
}

await using (var db = await app.Services.GetRequiredService<IDbContextFactory<SkySagaDbContext>>().CreateDbContextAsync())
    await db.Database.MigrateAsync();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/error", createScopeForErrors: true);

app.UseForwardedHeaders();
app.UseRequestLocalization("en-GB");
app.UseWhen(context => !context.Request.Path.StartsWithSegments("/api"),
    pages => pages.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapGameApi();
app.MapWorldsApi();
app.MapPost("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync();
    return Results.LocalRedirect("/");
});
app.MapRazorComponents<App>();

app.Run();
return 0;

static async Task ValidateStampAsync(CookieValidatePrincipalContext context)
{
    var accounts = context.HttpContext.RequestServices.GetRequiredService<AccountService>();
    var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
    var stamp = context.Principal?.FindFirstValue(AccountClaims.Stamp);
    var account = Guid.TryParse(id, out var accountId) ? await accounts.GetAccountAsync(accountId) : null;

    if (account is null || account.SecurityStamp.ToString() != stamp || account.BannedUntil > DateTimeOffset.UtcNow)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync();
    }
}
