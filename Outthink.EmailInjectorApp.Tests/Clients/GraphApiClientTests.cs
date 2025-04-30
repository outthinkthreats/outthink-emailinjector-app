using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using NSubstitute;
using OutThink.EmailInjectorApp.Clients;
using OutThink.EmailInjectorApp.Constants;
using OutThink.EmailInjectorApp.Interfaces;
using OutThink.EmailInjectorApp.Models;
using RichardSzalay.MockHttp;
using Xunit;

public class GraphApiClientTests
{
    private readonly IConfigurationService _config = Substitute.For<IConfigurationService>();
    private readonly ILoggingService _log = Substitute.For<ILoggingService>();

    private GraphApiClient CreateClient(MockHttpMessageHandler mockHttp)
    {
        var client = mockHttp.ToHttpClient();
        return new GraphApiClient(client, _config, _log);
    }

    [Fact]
    public async Task InjectEmailAsync_SendsExpectedRequest()
    {
        var mockHttp = new MockHttpMessageHandler();

        mockHttp.When(HttpMethod.Post, "https://graph.microsoft.com/v1.0/users/receiver@domain.com/mailFolders/inbox/messages")
                .WithHeaders("Authorization", "Bearer token")
                .Respond(HttpStatusCode.OK);

        var client = CreateClient(mockHttp);
        var msg = new DmiMessage(
            Guid.NewGuid(),
            "body",
            "sender@domain.com",
            "receiver@domain.com",
            "alias",
            "subject",
            new Dictionary<string, string> { { "X-Test", "1" } },
            MessageStatus.DmiEnqueued
        );

        await client.InjectEmailAsync(msg, "token");

        mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetUserObjectIdAsync_ReturnsExpectedId()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, "https://graph.microsoft.com/v1.0/users/user@domain.com")
                .WithHeaders("Authorization", "Bearer token")
                .Respond("application/json", """{"id":"abc-123"}""");

        var client = CreateClient(mockHttp);

        var id = await client.GetUserObjectIdAsync("user@domain.com", "token");

        Assert.Equal("abc-123", id);
    }

    [Fact]
    public async Task SendEmailAsync_ThrowsIfUserDoesNotExist()
    {
        var mockHttp = new MockHttpMessageHandler();

        mockHttp.When(HttpMethod.Get, "https://graph.microsoft.com/v1.0/users/missing@domain.com")
                .Respond(HttpStatusCode.NotFound);

        var client = CreateClient(mockHttp);
        var msg = new DmiMessage(
            Guid.NewGuid(),
            "body",
            "missing@domain.com",
            "anyone@domain.com",
            "alias",
            "subject",
            null,
            MessageStatus.GraphApiEnqueued
        );

        var ex = await Assert.ThrowsAsync<Exception>(() => client.SendEmailAsync(msg, "token"));
        Assert.Contains("User not found", ex.Message);
    }
}
