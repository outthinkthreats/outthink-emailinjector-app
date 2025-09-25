namespace OutThink.EmailInjectorApp.Interfaces;

public interface IHttpRequestService
{
    /// <summary>
    /// Sends an HTTP request to the given endpoint with optional JSON body.
    /// </summary>
    /// <param name="method">The HTTP method to use (GET, POST, etc).</param>
    /// <param name="endpoint">The relative API endpoint to call.</param>
    /// <param name="body">An optional object to serialize as JSON in the request body.</param>
    /// <returns>The <see cref="HttpResponseMessage"/> returned by the API.</returns>
    Task<HttpResponseMessage> SendAsync(HttpMethod method, string endpoint, object? body = null);
}