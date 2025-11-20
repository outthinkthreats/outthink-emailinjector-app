using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Identity.Client;
using OutThink.EmailInjectorApp.Constants;
using OutThink.EmailInjectorApp.Interfaces;
using MimeKit;
using System.Text.Json;
using System.Text;
using System.Net.Http.Headers;
using OutThink.EmailInjectorApp.Models;

namespace OutThink.EmailInjectorApp.Clients;

/// <summary>
/// Handles Microsoft Graph API operations for sending and injecting emails, retrieving tokens, and resolving user identities.
/// </summary>
public class GraphApiClient: IGraphApiClient
{
    private readonly HttpClient _client;
    private readonly IConfigurationService _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphApiClient"/> class.
    /// </summary>
    /// <param name="client">Injected <see cref="HttpClient"/> for API calls.</param>
    /// <param name="config">Configuration service to access app credentials.</param>
    /// <param name="loggingService">Logging service used during retry attempts.</param>
    public GraphApiClient(HttpClient client, IConfigurationService config, ILoggingService loggingService)
    {
        _client = client;
        _config = config;
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
    /// <exception cref="HttpRequestException">Thrown if an error occurs during the HTTP request.</exception>
    /// <exception cref="JsonException">Thrown if an error occurs while serializing the message content.</exception>
    /// <exception cref="Exception">Thrown for other general errors during email injection.</exception>
    public async Task InjectEmailAsync(DmiMessage msg, string token)
    {
        /* approach using flags */
        
        var test = new[] {
            new { id = "Integer 0x0E07", value = "1" },
            // new { id = "Integer 0x0E0B", value = "1" },  
            // new { id = "Integer 0x003F", value = "1" },
            // new { id = "Integer 0x1035", value = "1" } 
        };
        
        var payload = new
        {
            subject = msg.Subject,
            body = new { contentType = "HTML", content = msg.Body },
            from = new { emailAddress = new { name = msg.Alias, address = msg.From } },
            toRecipients = new[] { new { emailAddress = new { address = msg.To } } },
            isRead = false,
            internetMessageHeaders = msg.Headers?.Select(h => new { name = h.Key, value = h.Value }).ToArray(),
            attachments = msg.Attachments?.Select(a => new Dictionary<string, object>
                {
                    ["@odata.type"] = "#microsoft.graph.fileAttachment",
                    ["name"] = a.Name,
                    ["contentType"] = "application/pdf",
                    ["contentBytes"] = a.Data
                }
            ).ToArray(),
            singleValueExtendedProperties = test,
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"https://graph.microsoft.com/v1.0/users/{msg.To}/mailFolders/inbox/messages")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

// Important note: Replace this value with a REAL mailbox UPN that your app has permission to use as a sender (Send As).
    const string BUZON_REAL_DE_ENVIO = "nicolas.fernandez@outthink.io"; 

    public async Task InjectEmailMimeAsync(DmiMessage msg, string token)
    {
        // initial validation
        if (string.IsNullOrWhiteSpace(msg.To))
        {
            throw new InvalidOperationException("Recipient address (msg.To) cannot be empty or null.");
        }
    
        var mimeMessage = new MimeMessage();

        // 1. sender configuration (Authorized mailbox to pass validation)
        mimeMessage.From.Add(new MailboxAddress(msg.Alias, BUZON_REAL_DE_ENVIO));
    
        
        // 2. Sender configuration (Authorized mailbox to pass validation)
        try
        {
            var recipientList = InternetAddressList.Parse(msg.To);
            foreach(var address in recipientList.Mailboxes)
            {
                mimeMessage.To.Add(address);
            }
        }
        catch (ParseException ex)
        {
            throw new InvalidOperationException($"Recipient address(es) format is invalid: '{msg.To}'. Details: {ex.Message}");
        }
    
        mimeMessage.Subject = msg.Subject;

        // 3. Transport Headers and Custom Headers
    
        mimeMessage.Headers.Add("X-MS-Exchange-Organization-AuthAs", "Anonymous");
    
        mimeMessage.Headers.Add("X-Original-Sender", msg.From); 

        if (msg.Headers != null)
        {
            foreach (var header in msg.Headers)
            {
                mimeMessage.Headers.Add(header.Key, header.Value);
            }
        }

        var bodyBuilder = new BodyBuilder { HtmlBody = msg.Body };
    
        if (msg.Attachments != null)
        {
            foreach (var attachment in msg.Attachments)
            {
                byte[] fileBytes = Convert.FromBase64String(attachment.Data);
                var contentType = ContentType.Parse("application/pdf"); 
                bodyBuilder.Attachments.Add(attachment.Name, fileBytes, contentType);
            }
        }
        mimeMessage.Body = bodyBuilder.ToMessageBody();

        // serialize the MimeMessage to a Base64 string
        string mimeContentBase64;
        using (var memoryStream = new MemoryStream())
        {
            await mimeMessage.WriteToAsync(memoryStream);
            mimeContentBase64 = Convert.ToBase64String(memoryStream.ToArray());
        }
        
        // 4. Build the simple payload (only structure that does not cause schema errors)
    
        var payload = new
        {
            // Mime content is passed directly as the 'message' property of the send.
            message = new
            {
                // Only include 'internetMessageContent' if the error above was transient or
                // if the API version requires it.
                internetMessageContent = mimeContentBase64
            },
            saveToSentItems = false 
        };
    
        // 5. Send the email
        var request = new HttpRequestMessage(HttpMethod.Post, 
            $"https://graph.microsoft.com/v1.0/users/{BUZON_REAL_DE_ENVIO}/sendMail")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        var response = await _client.SendAsync(request);

        // Capture error details
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Graph API Error: {response.StatusCode} - {errorContent}"); 
        
            // If error is the same, it means the API does not allow MIME 
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest && 
                errorContent.Contains("'internetMessageContent' does not exist"))
            {
                throw new InvalidOperationException("Graph API rejected MIME structure. Cannot bypass Exchange Header Firewall via this endpoint.");
            }

            throw new HttpRequestException($"Response status code does not indicate success: {response.StatusCode}. Graph Detail: {errorContent}");
        }

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
                attachments = msg.Attachments?.Select(a => new Dictionary<string, object>
                    {
                        ["@odata.type"] = "#microsoft.graph.fileAttachment",
                        ["name"] = a.Name,
                        ["contentType"] = "application/pdf",
                        ["contentBytes"] = a.Data
                    }
                ).ToArray(),
                internetMessageHeaders = msg.Headers?.Select(h => new { name = h.Key, value = h.Value }).ToArray()
            },
            saveToSentItems = true
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"https://graph.microsoft.com/v1.0/users/{msg.From}/sendMail")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        var response = await _client.SendAsync(request);
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

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
    
        return doc.RootElement.GetProperty("id").GetString()!;
    }
}