using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace OutThink.EmailInjectorApp.Services;
public class HttpRequestService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;
    private readonly string _otApiKey;
    private readonly string _otCustomerId;

    public HttpRequestService(HttpClient httpClient, ConfigurationService configurationService, ILogger<HttpRequestService> logger)
    {
        _httpClient = httpClient;
        try
        {
            _apiBaseUrl = configurationService.Get("ApiBaseUrl");
            _otApiKey = configurationService.Get("OtApiKey");
            _otCustomerId = configurationService.Get("OtCustomerId");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while fetching configuration");
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string endpoint, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, $"{_apiBaseUrl}{endpoint}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _otApiKey);
        request.Headers.Add("OT-Customer-Id", _otCustomerId);

        if (content != null)
        {
            request.Content = content;
        }

        return request;
    }

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