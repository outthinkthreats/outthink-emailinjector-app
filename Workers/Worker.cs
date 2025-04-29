using OutThink.EmailInjectorApp.Constants;
using OutThink.EmailInjectorApp.Models;
using OutThink.EmailInjectorApp.Services;

namespace OutThink.EmailInjectorApp.Workers;

/// <summary>
/// Background worker that orchestrates message processing on a timed loop.
/// Reloads configuration before each cycle and handles execution errors gracefully.
/// </summary>
public class Worker : BackgroundService
{
    private readonly MessageProcessorService _processorService;
    private readonly LoggingService _log;
    private readonly ConfigurationService _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="Worker"/> class.
    /// </summary>
    /// <param name="log">Service for logging processing events and errors.</param>
    /// <param name="processorService">Service responsible for message processing logic.</param>
    /// <param name="config">Service providing application configuration and reload capability.</param>
    public Worker(LoggingService log, MessageProcessorService processorService, ConfigurationService config)
    {
        _log = log;
        _processorService = processorService;
        _config = config;
    }

    /// <summary>
    /// Main worker execution loop.
    /// Reloads configuration and processes campaigns, then waits before the next cycle.
    /// </summary>
    /// <param name="stoppingToken">Token to signal cancellation from host.</param>
    /// <returns>A task representing the background process execution.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _config.ReloadAsync();
                await _processorService.CheckAndProcessCampaignsAsync();
            }
            catch (Exception ex)
            {
                await _log.LogAsync("Unexpected error in campaign processing", [ex.Message], LogType.Error);
            }

            await WaitUntilNextCycleAsync(stoppingToken);
        }
    }

    /// <summary>
    /// Waits for the configured cycle delay duration before the next processing iteration.
    /// Falls back to 60 seconds if the configuration is invalid.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to support graceful shutdown.</param>
    private async Task WaitUntilNextCycleAsync(CancellationToken cancellationToken)
    {
        int delayMs = 60000;

        if (!int.TryParse(_config.Get(ConfigurationKeys.CycleDelay), out delayMs))
        {
            await _log.LogAsync("Invalid CycleDelay config, using default 60000ms", null, LogType.Warning);
        }

        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(delayMs), cancellationToken);
        }
        catch (OperationCanceledException) { }
    }

}