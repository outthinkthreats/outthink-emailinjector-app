using OutThink.EmailInjectorApp.Models;
using OutThink.EmailInjectorApp.Services;

namespace OutThink.EmailInjectorApp.Workers;

public class Worker : BackgroundService
{
    private readonly EmailInjectionService _emailService;
    private readonly LoggingService _loggingService;
    private readonly int _cycleDelay;
    private readonly ConfigurationService _configurationService;

    public Worker(LoggingService loggingService, EmailInjectionService emailService, ConfigurationService configurationService)
    {
        _loggingService = loggingService;
        _emailService = emailService;
        _configurationService = configurationService;
        _cycleDelay = int.Parse(configurationService.Get("CycleDelay") ?? "60000");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _emailService.CheckAndProcessCampaignsAsync();
            }
            catch (Exception ex)
            {
                await _loggingService.LogAsync("Error while processing campaigns", [ex.Message], LogType.Error);
            }
            await Task.Delay(_cycleDelay, stoppingToken);
        }
    }
}