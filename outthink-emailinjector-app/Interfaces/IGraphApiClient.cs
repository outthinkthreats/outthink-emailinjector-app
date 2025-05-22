using OutThink.EmailInjectorApp.Models;
using OutThink.EmailInjectorApp.Services;

namespace OutThink.EmailInjectorApp.Interfaces;

public interface IGraphApiClient
{
    Task<string> GetAccessTokenAsync();
    Task InjectEmailAsync(DmiMessage msg, string token);
    Task SendEmailAsync(DmiMessage msg, string token);
    Task<string> GetUserObjectIdAsync(string userPrincipalName, string token);
}