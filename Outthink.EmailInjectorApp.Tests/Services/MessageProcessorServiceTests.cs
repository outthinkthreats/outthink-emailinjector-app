using System.Net;
using System.Text;
using System.Text.Json;
using NSubstitute;
using OutThink.EmailInjectorApp.Constants;
using OutThink.EmailInjectorApp.Interfaces;
using OutThink.EmailInjectorApp.Models;
using OutThink.EmailInjectorApp.Services;
using Xunit;

namespace Outthink.EmailInjectorApp.Tests.Services;

public class MessageProcessorServiceTests
{
    private readonly IGraphApiClient _graph = Substitute.For<IGraphApiClient>();
    private readonly ILoggingService _log = Substitute.For<ILoggingService>();
    private readonly IConfigurationService _config = Substitute.For<IConfigurationService>();
    private readonly IHttpRequestService _http = Substitute.For<IHttpRequestService>();

    private MessageProcessorService CreateService() =>
        new(_config, _log, _graph, _http);

    [Fact]
    public async Task CheckAndProcessCampaignsAsync_LogsAndReturns_IfTokenIsNull()
    {
        _config.Get(ConfigurationKeys.BatchSize).Returns("1");
        _config.Get(ConfigurationKeys.SkipConfirmation).Returns("false");
        _graph.GetAccessTokenAsync().Returns((string?)null);

        await CreateService().CheckAndProcessCampaignsAsync();

        await _log.Received().LogAsync(
            "Access token is null or empty",
            null,
            LogType.Error);
    }

    [Fact]
    public async Task CheckAndProcessCampaignsAsync_ProcessesValidDmiMessage()
    {
        _config.Get(ConfigurationKeys.BatchSize).Returns("1");
        _config.Get(ConfigurationKeys.SkipConfirmation).Returns("true");
        _graph.GetAccessTokenAsync().Returns("valid-token");
        _graph.GetUserObjectIdAsync(Arg.Any<string>(), Arg.Any<string>()).Returns("user-123");

        var message = new DmiMessage(
            MessageId:Guid.NewGuid(),
            Body: "Test Body",
            From: "sender@domain.com",
            To: "test@domain.com",
            Alias: "test-alias",
            Subject: "Test Subject",
            Headers: new Dictionary<string, string> { { "test-key", "test-value" } },
            MessageStatus: MessageStatus.DmiEnqueued,
            Attachments: new List<MessageAttachment>
            {
                new ("test.txt", "test-data", "test-storage")
            }
        );

        var serialized = JsonSerializer.Serialize(message);
        var response = CreateStableResponse(serialized);

        _http.SendAsync(HttpMethod.Get, Arg.Any<string>(), null)
            .Returns(
                _ => Task.FromResult(CreateStableResponse(serialized)),
                _ => Task.FromResult(CreateStableResponse(""))
            );

        await CreateService().CheckAndProcessCampaignsAsync();

        await _graph.Received().InjectEmailAsync(
            Arg.Is<DmiMessage>(m =>
                m.MessageId == message.MessageId &&
                m.To == message.To &&
                m.From == message.From &&
                m.Body == message.Body &&
                m.Subject == message.Subject &&
                m.MessageStatus == message.MessageStatus
            ),
            "valid-token"
        );
        await _log.Received().LogAsync(
            Arg.Is<string>(s => s.Contains("Injected OK")),
            Arg.Any<string[]>(),
            LogType.Info);
    }

    private static HttpResponseMessage CreateStableResponse(string contentLine)
    {
        var mem = new MemoryStream(Encoding.UTF8.GetBytes(contentLine + "\n"));
        var content = new StreamContent(mem);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content
        };
        return response;
    }
}