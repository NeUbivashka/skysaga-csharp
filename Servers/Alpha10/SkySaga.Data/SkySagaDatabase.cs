using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SkySaga.Data;

public static class SkySagaDatabase
{
    public const string ConnectionVariable = "SKYSAGA_DB";
    public const string LocalConfigFile = "database.local.json";

    public static string ConnectionString(string dataDirectory)
    {
        if (Environment.GetEnvironmentVariable(ConnectionVariable) is { Length: > 0 } fromEnvironment)
            return fromEnvironment;

        var path = Path.Combine(dataDirectory, LocalConfigFile);

        if (!File.Exists(path))
            throw new InvalidOperationException($"No database configured: set {ConnectionVariable} or create {path}.");

        using var config = JsonDocument.Parse(File.ReadAllText(path));
        return config.RootElement.GetProperty("ConnectionString").GetString()
            ?? throw new InvalidOperationException($"{path} has no ConnectionString.");
    }

    public static DbContextOptions<SkySagaDbContext> Options(string connectionString)
        => new DbContextOptionsBuilder<SkySagaDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
}

public class SkySagaDbContextFactory : IDesignTimeDbContextFactory<SkySagaDbContext>
{
    public SkySagaDbContext CreateDbContext(string[] args)
        => new(SkySagaDatabase.Options(Environment.GetEnvironmentVariable(SkySagaDatabase.ConnectionVariable)
            ?? "Host=127.0.0.1;Database=skysaga;Username=skysaga"));
}
