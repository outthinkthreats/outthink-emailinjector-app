namespace OutThink.EmailInjectorApp.Models;

public record FailDmiMessage(Guid MessageId, string Reason, bool Retryable = true);
public record FailDmiMessages(IEnumerable<FailDmiMessage> Messages);