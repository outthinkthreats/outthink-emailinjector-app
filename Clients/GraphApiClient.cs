using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Identity.Client;
using OutThink.EmailInjectorApp.Constants;
using OutThink.EmailInjectorApp.Models;
using OutThink.EmailInjectorApp.Services;
using Polly;

namespace OutThink.EmailInjectorApp.Clients;

/// <summary>
/// Handles Microsoft Graph API operations for sending and injecting emails, retrieving tokens, and resolving user identities.
/// </summary>
public class GraphApiClient
{
    private readonly HttpClient _client;
    private readonly ConfigurationService _config;
    private readonly IAsyncPolicy<HttpResponseMessage> _retry;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphApiClient"/> class.
    /// </summary>
    /// <param name="client">Injected <see cref="HttpClient"/> for API calls.</param>
    /// <param name="config">Configuration service to access app credentials.</param>
    /// <param name="loggingService">Logging service used during retry attempts.</param>
    public GraphApiClient(HttpClient client, ConfigurationService config, LoggingService loggingService)
    {
        _client = client;
        _config = config;
        _retry = RetryPolicyFactory.CreateWithRetryAfter((result, time, attempt, ctx) =>
        {
            loggingService.LogAsync($"Retry {attempt}, status {result.Result?.StatusCode}, waiting {time.TotalSeconds} sec", null, LogType.Warning).Wait();
        });
    }

    /// <summary>
    /// Retrieves a valid Azure AD access token for Microsoft Graph API using client credentials flow.
    /// </summary>
    /// <returns>The access token string.</returns>
    public async Task<string> GetAccessTokenAsync()
    {
        var app = ConfidentialClientApplicationBuilder
            .Create(_config.Get( ConfigurationKeys.ClientId))
            .WithClientSecret(_config.Get(ConfigurationKeys.ClientSecret))
            .WithAuthority($"https://login.microsoftonline.com/{_config.Get(ConfigurationKeys.TenantId)}")
            .Build();

        var token = await app.AcquireTokenForClient(["https://graph.microsoft.com/.default"]).ExecuteAsync();
        return token.AccessToken;
    }

    /// <summary>
    /// Injects an email directly into the mailbox of the target user.
    /// </summary>
    /// <param name="msg">The message object containing email details.</param>
    /// <param name="token">Access token for authentication.</param>
    public async Task InjectEmailAsync(DmiMessage msg, string token)
    {
        var payload = new
        {
            subject = msg.Subject,
            body = new { contentType = "HTML", content = msg.Body },
            from = new { emailAddress = new { name = msg.Alias, address = msg.From } },
            toRecipients = new[] { new { emailAddress = new { address = msg.To } } },
            isRead = false,
            internetMessageHeaders = msg.Headers?.Select(h => new { name = h.Key, value = h.Value }).ToArray(),
            singleValueExtendedProperties = new[] { new { id = "Integer 0x0E07", value = "1" } }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"https://graph.microsoft.com/v1.0/users/{msg.To}/mailFolders/inbox/messages")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        var response = await _retry.ExecuteAsync(() => _client.SendAsync(request));
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Sends an email on behalf of a user using the Microsoft Graph <c>/sendMail</c> endpoint.
    /// Verifies the sender exists before sending.
    /// </summary>
    /// <param name="msg">The message object containing email details.</param>
    /// <param name="token">Access token for authentication.</param>
    /// <exception cref="Exception">Thrown if the sender user does not exist.</exception>
    public async Task SendEmailAsync(DmiMessage msg, string token)
    {
        // Validate sender
        var checkUser = new HttpRequestMessage(HttpMethod.Get, $"https://graph.microsoft.com/v1.0/users/{msg.From}")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) }
        };

        var userResp = await _client.SendAsync(checkUser);
        if (!userResp.IsSuccessStatusCode)
            throw new Exception($"User not found: {msg.From}");

        var payload = new
        {
            message = new
            {
                subject = msg.Subject,
                body = new { contentType = "HTML", content = msg.Body },
                toRecipients = new[] { new { emailAddress = new { address = msg.To } } },
                internetMessageHeaders = msg.Headers?.Select(h => new { name = h.Key, value = h.Value }).ToArray()
            },
            saveToSentItems = true
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"https://graph.microsoft.com/v1.0/users/{msg.From}/sendMail")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        var response = await _retry.ExecuteAsync(() => _client.SendAsync(request));
        response.EnsureSuccessStatusCode();
    }
    
    /// <summary>
    /// Retrieves the Azure AD ObjectId for a user given their principal name (email).
    /// </summary>
    /// <param name="userPrincipalName">The user's email or UPN.</param>
    /// <param name="token">Access token for authentication.</param>
    /// <returns>The user's Azure ObjectId as a string.</returns>
    public async Task<string> GetUserObjectIdAsync(string userPrincipalName, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://graph.microsoft.com/v1.0/users/{userPrincipalName}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _retry.ExecuteAsync(() => _client.SendAsync(request));
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
    
        return doc.RootElement.GetProperty("id").GetString()!;
    }
}
