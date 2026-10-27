using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using SkySaga.Admin.Components;
using SkySaga.Admin.Services;
using SkySaga.Data;

var builder = WebApplication.CreateBuilder(args);

var gameData = Path.GetFullPath(builder.Configuration["GameDataDirectory"] ?? "../SkySaga.Game/Data", builder.Environment.ContentRootPath);
var connectionString = SkySagaDatabase.ConnectionString(gameData);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddMudServices();
builder.Services.AddDbContextFactory<SkySagaDbContext>(options => options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());
builder.Services.AddSingleton(new GameDataDirectory(gameData));
builder.Services.AddSingleton<GameNames>();
builder.Services.AddSingleton(new AuthLinks((builder.Configuration["AuthUrl"] ?? "http://127.0.0.1:5100").TrimEnd('/')));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error", createScopeForErrors: true);

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
