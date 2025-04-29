using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OutThink.EmailInjectorApp.Constants;

namespace OutThink.EmailInjectorApp.Services;
/// <summary>
/// Provides a wrapper around <see cref="HttpClient"/> to send authenticated HTTP requests
/// using configuration values for headers and base URL.
/// </summary>
public class HttpRequestService
{
    private readonly HttpClient _httpClient;
    private readonly ConfigurationService _configurationService;
    private readonly ILogger<HttpRequestService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpRequestService"/> class.
    /// </summary>
    /// <param name="httpClient">Injected <see cref="HttpClient"/> instance.</param>
    /// <param name="configurationService">Injected service for resolving API configuration.</param>
    /// <param name="logger">Logger for error and request diagnostics.</param>
    public HttpRequestService(HttpClient httpClient, ConfigurationService configurationService, ILogger<HttpRequestService> logger)
    {
        _httpClient = httpClient;
        _configurationService = configurationService;
        _logger = logger;
    }

    /// <summary>
    /// Builds an <see cref="HttpRequestMessage"/> with the appropriate headers and content,
    /// using configuration for authentication and customer context.
    /// </summary>
    /// <param name="method">The HTTP method to use (GET, POST, etc).</param>
    /// <param name="endpoint">The relative API endpoint.</param>
    /// <param name="content">Optional HTTP body content.</param>
    /// <returns>An initialized <see cref="HttpRequestMessage"/> ready for sending.</returns>
    private HttpRequestMessage CreateRequest(HttpMethod method, string endpoint, HttpContent? content = null)
    {
        var apiBaseUrl = "";
        var otApiKey = "";
        var otCustomerId = "";
        
        try
        {
            apiBaseUrl = _configurationService.Get(ConfigurationKeys.ApiBaseUrl);
            otApiKey = _configurationService.Get(ConfigurationKeys.OtApiKey);
            otCustomerId = _configurationService.Get(ConfigurationKeys.OtCustomerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while fetching configuration");
        }
        
        var request = new HttpRequestMessage(method, $"{apiBaseUrl}{endpoint}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", otApiKey);
        request.Headers.Add("OT-Customer-Id", otCustomerId);

        if (content != null)
        {
            request.Content = content;
        }

        return request;
    }

    /// <summary>
    /// Sends an HTTP request to the given endpoint with optional JSON body.
    /// </summary>
    /// <param name="method">The HTTP method to use (GET, POST, etc).</param>
    /// <param name="endpoint">The relative API endpoint to call.</param>
    /// <param name="body">An optional object to serialize as JSON in the request body.</param>
    /// <returns>The <see cref="HttpResponseMessage"/> returned by the API.</returns>
    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string endpoint, object? body = null)
    {
        HttpContent? content = null;

        if (body != null)
        {
            var json = JsonSerializer.Serialize(body);
            content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        var request = CreateRequest(method, endpoint, content);
        return await _httpClient.SendAsync(request);
    }
}