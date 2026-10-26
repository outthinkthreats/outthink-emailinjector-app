using OutThink.EmailInjectorApp.Models;

namespace OutThink.EmailInjectorApp.Interfaces;

public interface ILoggingService
{
    /// <summary>
    /// Sends a log message to the remote logging API and also logs it locally.
    /// </summary>
    /// <param name="message">The log message text.</param>
    /// <param name="args">Optional message parameters.</param>
    /// <param name="logType">The log level (Info, Warning, Error).</param>
    /// <param name="exception">Optional exception to include in local and remote diagnostics.</param>
    Task LogAsync(
        string message,
        string[]? args = null,
        LogType logType = LogType.Info,
        Exception? exception = null);
}