namespace OutThink.EmailInjectorApp.Models;

public record DmiMessage
(
    Guid MessageId,
    string Body,
    string From,
    string To,
    string Alias,
    string Subject,
    Dictionary<string, string> Headers
);
