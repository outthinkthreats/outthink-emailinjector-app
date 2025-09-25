namespace OutThink.EmailInjectorApp.Interfaces;

public interface IMessageProcessorService
{
    /// <summary>
    /// Checks for pending messages and processes them.
    /// This includes message injection, sending, confirmation, and failure marking.
    /// </summary>
    Task CheckAndProcessCampaignsAsync();
}