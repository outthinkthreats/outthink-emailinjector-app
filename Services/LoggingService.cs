using System.Text;
using System.Text.Json;
using OutThink.EmailInjectorApp.Models;

namespace OutThink.EmailInjectorApp.Services
{
    public class LoggingService
    {
        private readonly HttpRequestService _httpRequestService;
        private readonly ILogger<LoggingService> _logger;
        private readonly string? _appName;

        public LoggingService(HttpRequestService httpRequestService, ILogger<LoggingService> logger, IConfiguration config)
        {
            _httpRequestService = httpRequestService;
            _logger = logger;
            _appName = config["app:service"];
        }

        public async Task LogAsync(string message, string[]? args = null, LogType logType = LogType.Info)
        {
            args ??= [];
            var logEntry = new RegisterLog(_appName, message, args, logType);

            try
            {
                var response = await _httpRequestService.SendAsync(HttpMethod.Post, "/log", logEntry);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }
    }
}