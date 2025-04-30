using System.Net;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using OutThink.EmailInjectorApp.Interfaces;
using OutThink.EmailInjectorApp.Models;
using OutThink.EmailInjectorApp.Services;
using Outthink.EmailInjectorApp.Tests.Helpers;
using Xunit;

namespace Outthink.EmailInjectorApp.Tests.Services;

public class LoggingServiceTests
{
    private readonly IHttpRequestService _httpRequestService = Substitute.For<IHttpRequestService>();
    private readonly IConfiguration _config;
    private readonly FakeLogger<LoggingService> _logger = new();

    public LoggingServiceTests()
    {
        var inMemory = new Dictionary<string, string?>
        {
            { "app:service", "TestService" }
        };
        _config = new ConfigurationBuilder().AddInMemoryCollection(inMemory!).Build();
    }

    private LoggingService CreateService() =>
        new LoggingService(_httpRequestService, _logger, _config);

    [Fact]
    public async Task LogAsync_SendsLog_RemotelyAndLocally_Info()
    {
        var service = CreateService();
        _httpRequestService.SendAsync(HttpMethod.Post, "/log", Arg.Any<RegisterLog>())
            .Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        await service.LogAsync("Hello log", new[] { "param1", "param2" }, LogType.Info);

        Assert.Contains(_logger.Logs, log => log.Contains("Hello log"));
    }

    [Fact]
    public async Task LogAsync_HandlesRemoteFailure_AndStillLogsLocally()
    {
        var service = CreateService();
        _httpRequestService.SendAsync(HttpMethod.Post, "/log", Arg.Any<RegisterLog>())
            .Returns<Task<HttpResponseMessage>>(x => throw new Exception("Remote API down"));

        await service.LogAsync("Critical log", null, LogType.Error);

        Assert.Contains(_logger.Logs, log => log.Contains("Critical log"));
        Assert.Contains(_logger.Logs, log => log.Contains("Remote API down"));
    }

    [Theory]
    [InlineData(LogType.Info)]
    [InlineData(LogType.Warning)]
    [InlineData(LogType.Error)]
    public async Task LogAsync_LogsLocally_WithExpectedContent(LogType logType)
    {
        var service = CreateService();
        _httpRequestService.SendAsync(HttpMethod.Post, "/log", Arg.Any<RegisterLog>())
            .Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        await service.LogAsync("Expected content", null, logType);

        Assert.Contains(_logger.Logs, log => log.Contains("Expected content"));
    }
}