using OutThink.EmailInjectorApp.Constants;

namespace OutThink.EmailInjectorApp.Models;

public record MessageAttachment(string Name, string Data, string StorageLocation);

public record DmiMessage
(
    Guid MessageId,
    string Body,
    string From,
    string To,
    string Alias,
    string Subject,
    Dictionary<string, string> Headers,
    MessageStatus MessageStatus, 
    IEnumerable<MessageAttachment> Attachments
);
