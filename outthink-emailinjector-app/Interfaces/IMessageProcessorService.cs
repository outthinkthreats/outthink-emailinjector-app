namespace OutThink.EmailInjectorApp.Interfaces;

public interface IMessageProcessorService
{
    Task CheckAndProcessCampaignsAsync();
}