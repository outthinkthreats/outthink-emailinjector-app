using System.Text;
using System.Text.Json;
using OutThink.EmailInjectorApp.Interfaces;
using OutThink.EmailInjectorApp.Models;

namespace OutThink.EmailInjectorApp.Services
{

    public class OutThink
    {
    }

    /// <summary>
    /// Handles logging messages to both a remote logging endpoint and the local application log stream.
    /// </summary>
    public class LoggingService: ILoggingService
    {
        private readonly IHttpRequestService _httpRequestService;
        private readonly ILogger _logger;
        private readonly string _appName;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoggingService"/> class.
        /// </summary>
        /// <param name="httpRequestService">Service used to send logs to a remote logging endpoint.</param>
        /// <param name="logger">Local logger for internal application logs.</param>
        /// <param name="loggerFactory"></param>
        /// <param name="config">Application configuration to resolve app name.</param>
        public LoggingService(IHttpRequestService httpRequestService, ILoggerFactory loggerFactory, IConfiguration config)
        {
            _logger = loggerFactory.CreateLogger("OutThink");
            _httpRequestService = httpRequestService;
            _appName = config["app:service"] ?? "EmailInjectorApp";
        }

        /// <summary>
        /// Sends a log message to the remote logging API and also logs it locally.
        /// </summary>
        /// <param name="message">The log message text.</param>
        /// <param name="args">Optional message parameters.</param>
        /// <param name="logType">The log level (Info, Warning, Error).</param>
        /// <param name="exception">Optional exception to include in local and remote diagnostics.</param>
        public async Task LogAsync(
            string message,
            string[]? args = null,
            LogType logType = LogType.Info,
            Exception? exception = null)
        {
            var diagnosticArgs = args?.ToList() ?? [];
            if (exception is not null)
            {
                diagnosticArgs.Add($"Exception type: {exception.GetType().FullName}");
                diagnosticArgs.Add($"Exception message: {exception.Message}");

                if (exception.InnerException is not null)
                {
                    diagnosticArgs.Add($"Inner exception: {exception.InnerException.Message}");
                }
            }

            var logEntry = new RegisterLog(_appName, message, diagnosticArgs.ToArray(), logType);

            try
            {
                var response = await _httpRequestService.SendAsync(HttpMethod.Post, "/log", logEntry);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
            finally
            {
                LogLocally(message, diagnosticArgs, logType, exception);
            }
        }

        /// <summary>
        /// Writes a log entry to the local logger using the appropriate log level.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="args">Optional diagnostic details.</param>
        /// <param name="logType">The log level to use.</param>
        /// <param name="exception">Optional exception for stack trace logging.</param>
        private void LogLocally(string message, IReadOnlyCollection<string> args, LogType logType, Exception? exception)
        {
            var localMessage = args.Count == 0
                ? message
                : $"{message}. Details: {string.Join(" | ", args)}";

            switch (logType)
            {
                case LogType.Info:
                    _logger.LogInformation(localMessage);
                    break;
                case LogType.Warning:
                    _logger.LogWarning(localMessage);
                    break;
                case LogType.Error:
                    _logger.LogError(exception, "{LogMessage}", localMessage);
                    break;
                case LogType.Debug:
                    _logger.LogDebug(localMessage);
                    break;
                default:
                    _logger.LogInformation(localMessage);
                    break;
            }
        }
    }
}