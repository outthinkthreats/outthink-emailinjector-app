using OutThink.EmailInjectorApp.Models;
using OutThink.EmailInjectorApp.Services;

namespace OutThink.EmailInjectorApp.Interfaces;

public interface IGraphApiClient
{
    /// <summary>
    /// Retrieves a valid Azure AD access token for Microsoft Graph API using client credentials flow.
    /// </summary>
    /// <returns>The access token string.</returns>
    Task<string> GetAccessTokenAsync();
    /// <summary>
    /// Injects an email directly into the mailbox of the target user.
    /// </summary>
    /// <param name="msg">The message object containing email details.</param>
    /// <param name="token">Access token for authentication.</param>
    Task InjectEmailAsync(DmiMessage msg, string token);
    /// <summary>
    /// Sends an email on behalf of a user using the Microsoft Graph <c>/sendMail</c> endpoint.
    /// Verifies the sender exists before sending.
    /// </summary>
    /// <param name="msg">The message object containing email details.</param>
    /// <param name="token">Access token for authentication.</param>
    /// <exception cref="Exception">Thrown if the sender user does not exist.</exception>
    Task SendEmailAsync(DmiMessage msg, string token);
    /// <summary>
    /// Retrieves the Azure AD ObjectId for a user given their principal name (email).
    /// </summary>
    /// <param name="userPrincipalName">The user's email or UPN.</param>
    /// <param name="token">Access token for authentication.</param>
    /// <returns>The user's Azure ObjectId as a string.</returns>
    Task<string> GetUserObjectIdAsync(string userPrincipalName, string token);
}