namespace OutThink.EmailInjectorApp.Interfaces;

public interface IHttpRequestService
{
    Task<HttpResponseMessage> SendAsync(HttpMethod method, string endpoint, object? body = null);
}