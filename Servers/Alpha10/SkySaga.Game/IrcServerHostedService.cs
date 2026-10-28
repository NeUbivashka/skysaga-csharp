namespace SkySaga.Game;

public class IrcServerHostedService(
    IrcServer ircServer,
    Server server,
    ILogger<IrcServerHostedService> logger) : IHostedService
{
    private readonly IrcServer _ircServer = ircServer ?? throw new ArgumentNullException(nameof(ircServer));
    private readonly ILogger<IrcServerHostedService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting IRC server...");
        _ircServer.CommandHandler = server.RunCommandAsync;
        _ircServer.BlockersHandler = Database.SocialStore.BlockersOfAsync;
        _ = _ircServer.StartAsync();
        _logger.LogInformation("IRC server started");

        return;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping IRC server...");
        await _ircServer.StopAsync();
        _logger.LogInformation("IRC server stopped");
    }
}
