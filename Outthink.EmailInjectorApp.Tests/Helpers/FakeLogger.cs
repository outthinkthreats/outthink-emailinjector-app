using Microsoft.Extensions.Logging;

namespace Outthink.EmailInjectorApp.Tests.Helpers;

public class FakeLogger<T> : ILogger<T>
{
    public List<string> Logs { get; } = new();

    public IDisposable BeginScope<TState>(TState state) => default!;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state,
        Exception exception, Func<TState, Exception?, string> formatter)
    {
        Logs.Add(formatter(state, exception));
    }
}
