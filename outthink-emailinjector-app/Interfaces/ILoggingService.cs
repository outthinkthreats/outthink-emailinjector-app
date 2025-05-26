using OutThink.EmailInjectorApp.Models;

namespace OutThink.EmailInjectorApp.Interfaces;

public interface ILoggingService
{
    Task LogAsync(string message, string[]? args = null, LogType logType = LogType.Info);
}