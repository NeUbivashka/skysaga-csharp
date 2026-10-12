
Console.SetOut(new TimestampedTextWriter(Console.Out));

if (!File.Exists("RakNet.dll"))
{
    Console.WriteLine("""
        The RakNet DLL is missing.
        Press any key to continue . . .
        """);

    Console.ReadKey();

    return;
}

try
{
    _ = new RakString();
}
catch
{
    Console.WriteLine("""
        RakNet DLL issue.
        Most likely the provided DLL wasn't build with the C# wrapper file.
        Press any key to continue . . .
        """);

    Console.ReadKey();

    return;
}

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton<PlayerConnectionManager>();
        services.AddSingleton<IWorldManager, WorldManager>();
        services.AddSingleton(sp => new PlayerInitializer(sp.GetRequiredService<IWorldManager>()));

        services.AddSingleton<Server>();

        services.AddSingleton<IrcServer>();

        services.AddHostedService<ServerHostedService>();
        services.AddHostedService<IrcServerHostedService>();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();

        logging.SetMinimumLevel(LogLevel.Information);
    });

var host = builder.Build();

await host.RunAsync();

Console.WriteLine("""
        Server has stopped.
        Press any key to continue . . .
        """);

Console.ReadKey();

sealed class TimestampedTextWriter(TextWriter inner) : TextWriter
{
    public override System.Text.Encoding Encoding => inner.Encoding;

    public override void WriteLine(string? value) =>
        inner.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {value}");

    public override void Write(char value) => inner.Write(value);
    public override void Write(string? value) => inner.Write(value);
    public override void Flush() => inner.Flush();
}