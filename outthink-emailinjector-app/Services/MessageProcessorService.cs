using System.Net;
using System.Text.Json;
using OutThink.EmailInjectorApp.Constants;
using OutThink.EmailInjectorApp.Interfaces;
using OutThink.EmailInjectorApp.Models;

namespace OutThink.EmailInjectorApp.Services;

/// <summary>
/// Service responsible for processing pending messages.
/// Handles message injection, sending, status updates, and error logging.
/// </summary>
public class MessageProcessorService(
    IConfigurationService config,
    ILoggingService log,
    IGraphApiClient graph,
    IHttpRequestService http): IMessageProcessorService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Checks for pending messages and processes them.
    /// This includes message injection, sending, confirmation, and failure marking.
    /// </summary>
    public async Task CheckAndProcessCampaignsAsync()
    {
        var batchSize = int.Parse(config.Get(ConfigurationKeys.BatchSize));
        var skipConfirmation = bool.Parse(config.Get(ConfigurationKeys.SkipConfirmation));
        
        bool hasMore;
        do
        {
            hasMore = false;
            var toConfirm = new List<Guid>();
            var toFail = new List<FailDmiMessage>();
            var token = await graph.GetAccessTokenAsync();
            
            if (string.IsNullOrWhiteSpace(token))
            {
                await log.LogAsync("Access token is null or empty", null, LogType.Error);
                return;
            }

            await foreach (var msg in GetPendingMessagesAsync(batchSize, skipConfirmation))
            {
                hasMore = true;
                await ProcessMessageAsync(msg, token, toConfirm, toFail);
            }

            if (!skipConfirmation && toConfirm.Any())
                await ConfirmInjectedMessagesAsync(toConfirm);

            if (toFail.Any())
                await FailInjectedMessagesAsync(new FailDmiMessages(toFail));

        } while (hasMore);
    }
    
    /// <summary>
    /// Processes a single message, attempting injection or sending based on message status.
    /// Logs the outcome and updates confirmation or failure lists.
    /// </summary>
    /// <param name="msg">The message to process.</param>
    /// <param name="token">Graph API access token.</param>
    /// <param name="toConfirm">List of message IDs to confirm as injected.</param>
    /// <param name="toFail">List of failed message entries to register as failed.</param>
    private async Task ProcessMessageAsync(DmiMessage msg, string token, List<Guid> toConfirm, List<FailDmiMessage> toFail)
    {
        try
        {
            switch (msg.MessageStatus)
            {
                case MessageStatus.DmiEnqueued:
                    var userObjectId = await graph.GetUserObjectIdAsync(msg.To, token);
                    await graph.InjectEmailAsync(msg, token);
                    await log.LogAsync($"Injected OK (userObjectId: {userObjectId})");
                    break;
                case MessageStatus.GraphApiEnqueued:
                    await graph.SendEmailAsync(msg, token);
                    await log.LogAsync($"Sent OK (userObjectId: {msg.To[..5]})");
                    break;
                default:
                    await log.LogAsync($"Invalid status for {msg.MessageId}: {msg.MessageStatus}", null, LogType.Warning);
                    return;
            }

            toConfirm.Add(msg.MessageId);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            await log.LogAsync($"Not found: {msg.To}", null, LogType.Warning);
            toFail.Add(new FailDmiMessage(msg.MessageId, "Mailbox not found", false));
        }
        catch (Exception ex)
        {
            await log.LogAsync($"FAIL: {ex.Message} (userObjectId: {msg.From})", null, LogType.Error);
            toFail.Add(new FailDmiMessage(msg.MessageId, ex.Message, true));
        }
    }
    
    /// <summary>
    /// Streams pending messages from the backend API.
    /// Supports large batches via streaming without full memory load.
    /// </summary>
    /// <param name="batchSize">Max number of messages to fetch per request.</param>
    /// <param name="skipConfirmation">Whether to skip confirmation step.</param>
    /// <returns>Async stream of <see cref="DmiMessage"/> instances.</returns>
    private async IAsyncEnumerable<DmiMessage> GetPendingMessagesAsync(int batchSize, bool skipConfirmation)
    {
        var url = $"/communications/messages/dmi/pending/stream?batchSize={batchSize}&skipConfirmation={skipConfirmation}";
    
        using var response = await http.SendAsync(HttpMethod.Get, url);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            DmiMessage? msg;
            try
            {
                msg = JsonSerializer.Deserialize<DmiMessage>(line, _jsonOptions);
            }
            catch (JsonException ex)
            {
                await log.LogAsync($"JSON deserialization failed: {ex.Message}", null, LogType.Warning);
                continue;
            }

            if (msg is not null) yield return msg;
            else await log.LogAsync($"Invalid Message JSON (null result)", null, LogType.Warning);
        }
    }

    /// <summary>
    /// Confirms successful injection of messages by their IDs.
    /// </summary>
    /// <param name="ids">List of successfully injected message IDs.</param>
    private async Task ConfirmInjectedMessagesAsync(IEnumerable<Guid> ids)
    {
        try
        {
            var resp = await http.SendAsync(HttpMethod.Post, "/communications/messages/dmi/confirm", new { MessageIds = ids });
            resp.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            await log.LogAsync("Confirm failed", [ex.Message], LogType.Error);
        }
    }

    /// <summary>
    /// Registers failed messages with reason and type (transient/permanent).
    /// </summary>
    /// <param name="failures">Collection of failure details.</param>
    private async Task FailInjectedMessagesAsync(FailDmiMessages failures)
    {
        try
        {
            var resp = await http.SendAsync(HttpMethod.Post, "/communications/messages/dmi/fail", failures);
            resp.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            await log.LogAsync("Fail marking failed", [ex.Message], LogType.Error);
        }
    }
}
