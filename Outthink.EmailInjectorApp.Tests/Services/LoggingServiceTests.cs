using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OutThink.EmailInjectorApp.Interfaces;
using OutThink.EmailInjectorApp.Models;
using OutThink.EmailInjectorApp.Services;
using Xunit;

namespace Outthink.EmailInjectorApp.Tests.Services;

public class LoggingServiceTests
{
    private readonly IHttpRequestService _httpRequestService = Substitute.For<IHttpRequestService>();
    private readonly IConfiguration _config;
    private readonly FakeLogger _fakeLogger = new();
    private readonly ILoggerFactory _loggerFactory;

    public LoggingServiceTests()
    {
        var inMemory = new Dictionary<string, string?>
        {
            { "app:service", "TestService" }
        };
        _config = new ConfigurationBuilder().AddInMemoryCollection(inMemory!).Build();

        _loggerFactory = Substitute.For<ILoggerFactory>();
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(_fakeLogger);
    }

    private LoggingService CreateService() =>
        new LoggingService(_httpRequestService, _loggerFactory, _config);

    [Fact]
    public async Task LogAsync_SendsLog_RemotelyAndLocally_Info()
    {
        var service = CreateService();
        _httpRequestService.SendAsync(HttpMethod.Post, "/log", Arg.Any<RegisterLog>())
            .Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        await service.LogAsync("Hello log", new[] { "param1", "param2" }, LogType.Info);

        Assert.Contains(_fakeLogger.Logs, log => log.Contains("Hello log"));
    }

    [Fact]
    public async Task LogAsync_HandlesRemoteFailure_AndStillLogsLocally()
    {
        var service = CreateService();
        _httpRequestService.SendAsync(HttpMethod.Post, "/log", Arg.Any<RegisterLog>())
            .Returns<Task<HttpResponseMessage>>(x => throw new Exception("Remote API down"));

        await service.LogAsync("Critical log", null, LogType.Error);

        Assert.Contains(_fakeLogger.Logs, log => log.Contains("Critical log"));
        Assert.Contains(_fakeLogger.Logs, log => log.Contains("Remote API down"));
    }

    [Theory]
    [InlineData(LogType.Info)]
    [InlineData(LogType.Warning)]
    [InlineData(LogType.Error)]
    [InlineData(LogType.Debug)]
    public async Task LogAsync_LogsLocally_WithExpectedContent(LogType logType)
    {
        var service = CreateService();
        _httpRequestService.SendAsync(HttpMethod.Post, "/log", Arg.Any<RegisterLog>())
            .Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        await service.LogAsync("Expected content", null, logType);

        Assert.Contains(_fakeLogger.Logs, log => log.Contains("Expected content"));
    }
}

public class FakeLogger : ILogger
{
    public List<string> Logs { get; } = new();

    public IDisposable BeginScope<TState>(TState state) => default!;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state,
        Exception exception, Func<TState, Exception?, string> formatter)
    {
        Logs.Add(formatter(state, exception));
    }
}
