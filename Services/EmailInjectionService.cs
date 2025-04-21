using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Identity.Client;
using OutThink.EmailInjectorApp.Enums;
using OutThink.EmailInjectorApp.Models;
using Polly;

namespace OutThink.EmailInjectorApp.Services
{
    public class EmailInjectionService
    {
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _tenantId;
        private readonly HttpClient _httpClient;
        private readonly LoggingService _loggingService;
        private readonly HttpRequestService _httpRequestService;
        private readonly int _batchSize;
        private readonly bool _skipConfirmation;

        public EmailInjectionService(ConfigurationService configurationService, HttpRequestService httpRequestService, LoggingService loggingService, HttpClient httpClient)
        {
            _httpRequestService = httpRequestService;
            _loggingService = loggingService;
            
            _loggingService.LogAsync("Starting Email Injection Service").Wait();

            _httpClient = httpClient;
            _clientId = configurationService.Get("ClientId");
            _clientSecret = configurationService.Get("ClientSecret");
            _tenantId = configurationService.Get("TenantId");
            _batchSize = int.Parse(configurationService.Get("BatchSize"));
            _skipConfirmation = bool.Parse(configurationService.Get("SkipConfirmation"));
        }

        public async Task<string> GetAccessTokenForGraphApiAsync()
        {
            var app = ConfidentialClientApplicationBuilder.Create(_clientId)
                .WithClientSecret(_clientSecret)
                .WithAuthority($"https://login.microsoftonline.com/{_tenantId}")
                .Build();

            var result = await app.AcquireTokenForClient(["https://graph.microsoft.com/.default"]).ExecuteAsync();
            return result.AccessToken;
        }
        
        private async IAsyncEnumerable<DmiMessage> GetPendingMessages()
        {
            using var response = await _httpRequestService.SendAsync(HttpMethod.Get, $"/communications/messages/dmi/pending/stream?batchSize={_batchSize}&skipConfirmation={_skipConfirmation.ToString()}");
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            while (await reader.ReadLineAsync() is { } line)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    yield return JsonSerializer.Deserialize<DmiMessage>(line, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    })!;
                }
            }
        }


        public async Task CheckAndProcessCampaignsAsync()
        {
            bool pendingMessages;
            
            do
            {
                var messageToBeconfirmed = new List<Guid>();
                var messageToBeFailed = new List<FailDmiMessage>();
                pendingMessages = false;
                var token = await GetAccessTokenForGraphApiAsync();
                await foreach (var msg in GetPendingMessages())
                {
                    pendingMessages = true;
                    
                    try
                    {
                        if (msg.MessageStatus == MessageStatus.DmiEnqueued) {
                            await InjectEmailWithRetryAsync(msg.To, msg.Body, token, msg.From, msg.Alias, msg.Subject,
                                msg.Headers);
                        }
                        else if (msg.MessageStatus == MessageStatus.GraphApiEnqueued)
                        {
                            await SendEmailWithRetryAsync(msg.To, msg.Body, token, msg.From, msg.Alias, msg.Subject,
                                msg.Headers);
                        }
                        else
                        {
                            _loggingService.LogAsync($"Message {msg.MessageId} has an invalid status {msg.MessageStatus}.", null, LogType.Warning).Wait();
                            continue;
                        }
                        messageToBeconfirmed.Add(msg.MessageId);
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                    {
                        _loggingService.LogAsync($"Mailbox not found for email {msg.To}. Skipping.", null, LogType.Warning).Wait();
                        messageToBeFailed.Add(new FailDmiMessage(msg.MessageId, "Mailbox not found", false));
                    }
                    catch (HttpRequestException ex)
                    {
                        _loggingService.LogAsync($"Final failure after retries. HTTP error: {ex.Message}", null, LogType.Error).Wait();
                        messageToBeFailed.Add(new FailDmiMessage(msg.MessageId, ex.Message, true));
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogAsync($"Unexpected error injecting email: {ex.Message}", null, LogType.Error).Wait();
                        messageToBeFailed.Add(new FailDmiMessage(msg.MessageId, ex.Message, true));
                    }
                    // if we are not autoconfirming on reception (SkipConfirmation is false), we need to confirm the message
                    if (!_skipConfirmation && messageToBeconfirmed.Any()) await ConfirmInjectedMessagesAsync(messageToBeconfirmed);
                    if (messageToBeFailed.Any()) await FailInjectedMessagesAsync(new FailDmiMessages(messageToBeFailed));
                }
            } while (pendingMessages);
        }
        
        private async Task ConfirmInjectedMessagesAsync(IEnumerable<Guid> messageIds)
        {
            var confirmation = new { MessageIds = messageIds };

            try
            {
                var response = await _httpRequestService.SendAsync(HttpMethod.Post,
                    "/communications/messages/dmi/confirm", confirmation);
                response.EnsureSuccessStatusCode();
                _loggingService.LogAsync($"{messageIds.Count()} message confirmed", logType: LogType.Info).Wait();
            }
            catch (Exception ex)
            {
                _loggingService.LogAsync("Error while confirming messages", [ex.Message], LogType.Error).Wait();
            }
        }
        
        
        
        private async Task FailInjectedMessagesAsync(FailDmiMessages failures)
        {
            try
            {
                var response = await _httpRequestService.SendAsync(HttpMethod.Post,
                    "/communications/messages/dmi/fail", failures);
                response.EnsureSuccessStatusCode();
                _loggingService.LogAsync($"{failures.Messages.Count()} messages failed", logType: LogType.Warning).Wait();
            }
            catch (Exception ex)
            {
                _loggingService.LogAsync("Error while failing messages", [ex.Message], LogType.Error).Wait();
            }
        }

        private async Task InjectEmailWithRetryAsync(string email, string htmlBody, string token, string fromEmail,
            string fromName, string subject, Dictionary<string, string> headers)
        {
            var message = new
            {
                subject,
                body = new { contentType = "HTML", content = htmlBody },
                from = new { emailAddress = new { name = fromName, address = fromEmail } },
                toRecipients = new[] { new { emailAddress = new { address = email } } },
                isRead = false,
                internetMessageHeaders =
                    (headers ?? new Dictionary<string, string>()).Select(x => new { name = x.Key, value = x.Value }).ToArray(),
                singleValueExtendedProperties = new[]
                {
                    new { id = "Integer 0x0E07", value = "1" }
                },
            };
            
            

            var request = new HttpRequestMessage(HttpMethod.Post, $"https://graph.microsoft.com/v1.0/users/{email}/mailFolders/inbox/messages");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(JsonSerializer.Serialize(message), Encoding.UTF8, "application/json");

            var retryPolicy = GetRetryPolicy();

            var response = await retryPolicy.ExecuteAsync(async (context) =>
            {
                var result = await _httpClient.SendAsync(request);
                context["response"] = result;

                if (result.StatusCode == HttpStatusCode.NotFound) // 404 is not recoverable
                {
                    throw new HttpRequestException($"Mailbox not found for email {email}", null, HttpStatusCode.NotFound);
                }

                return result;
            }, new Context());

            response.EnsureSuccessStatusCode();
        }

        private async Task SendEmailWithRetryAsync(string email, string htmlBody, string token, string fromEmail,
            string fromName, string subject, Dictionary<string, string> headers)
        {
            if (string.IsNullOrWhiteSpace(fromEmail))
                throw new ArgumentException("Sender email cannot be null or empty", nameof(fromEmail));
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Recipient email cannot be null or empty", nameof(email));

            _loggingService.LogAsync("Resolving user for sender email: {FromEmail}", [fromEmail], LogType.Error);

            // Validate sender exists
            var userLookupRequest = new HttpRequestMessage(HttpMethod.Get, $"https://graph.microsoft.com/v1.0/users/{fromEmail}");
            userLookupRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var userLookupResponse = await _httpClient.SendAsync(userLookupRequest);
            if (!userLookupResponse.IsSuccessStatusCode)
            {
                _loggingService.LogAsync("Failed to resolve sender user: {FromEmail}. Status code: {StatusCode}", [fromEmail, userLookupResponse.StatusCode.ToString()], LogType.Error);
                throw new Exception($"User not found for email: {fromEmail}");
            }

            _loggingService.LogAsync("Sender user {FromEmail} found. Preparing email to {ToEmail}", [fromEmail, email], LogType.Error);

            var payload = new
            {
                message = new
                {
                    subject,
                    body = new { contentType = "HTML", content = htmlBody },
                    toRecipients = new[] { new { emailAddress = new { address = email } } },
                    internetMessageHeaders = headers?.Select(h => new { name = h.Key, value = h.Value }).ToArray()
                },
                saveToSentItems = true
            };

            using var sendMailRequest = new HttpRequestMessage(HttpMethod.Post, $"https://graph.microsoft.com/v1.0/users/{fromEmail}/sendMail")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            sendMailRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var retryPolicy = GetRetryPolicy();

            var response = await retryPolicy.ExecuteAsync(async context =>
            {
                var result = await _httpClient.SendAsync(sendMailRequest);
                context["response"] = result;

                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    _loggingService.LogAsync("SendMail failed: sender {FromEmail} not found or endpoint missing", [fromEmail], LogType.Error);
                    throw new HttpRequestException("SendMail endpoint or sender not found", null, HttpStatusCode.NotFound);
                }

                return result;
            }, new Context());

            response.EnsureSuccessStatusCode();

            _loggingService.LogAsync("Email successfully sent to {ToEmail} from {FromEmail}", [email, fromEmail], LogType.Error);
        }


        private IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return Policy
                .HandleResult<HttpResponseMessage>(r => 
                    r.StatusCode == HttpStatusCode.TooManyRequests || 
                    r.StatusCode == HttpStatusCode.ServiceUnavailable)
                .WaitAndRetryAsync(
                    retryCount: 5,
                    sleepDurationProvider: (retryAttempt, context) =>
                    {
                        if (context.TryGetValue("response", out var responseObj) && responseObj is HttpResponseMessage response)
                        {
                            if (response.Headers.TryGetValues("Retry-After", out var values) &&
                                int.TryParse(values.FirstOrDefault() ?? "10", out int retrySeconds))
                            {
                                return TimeSpan.FromSeconds(retrySeconds);
                            }
                        }
                        return TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)); // exponencial Backoff when no Retry-After
                    },
                    onRetry: (delegateResult, timespan, retryAttempt, context) =>
                    {
                        var statusCode = delegateResult.Result?.StatusCode ?? HttpStatusCode.InternalServerError;
                        _loggingService.LogAsync(
                            $"Throttling detected (attempt {retryAttempt}, Status: {statusCode}). Retrying in {timespan.TotalSeconds} seconds...", 
                            null, LogType.Warning).Wait();
                    }
                );
        }

    }
}