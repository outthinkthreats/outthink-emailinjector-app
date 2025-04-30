namespace OutThink.EmailInjectorApp.Constants;

public enum MessageStatus
{
    Undefined = 0,
    Enqueued = 1,
    Sent = 2,
    Retrying = 3,
    Failed = 4,
    InvalidDomain = 5,
    Duplicate = 6,
    Stale = 7,
    DmiEnqueued = 8,
    DmiSending = 9,
    DmiDelivered = 10,
    GraphApiEnqueued = 11,
    GraphApiSending = 12,
    GraphApiDelivered = 13,
}