using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging.ApplicationInsights;
using OutThink.EmailInjectorApp.Clients;
using OutThink.EmailInjectorApp.Interfaces;
using OutThink.EmailInjectorApp.Services;
using OutThink.EmailInjectorApp.Workers;

var builder = WebApplication.CreateBuilder(args);



// Load configuration
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables();

// Enable Application Insights
builder.Logging.AddFilter<ApplicationInsightsLoggerProvider>("", LogLevel.Information);
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("System", LogLevel.Warning);
builder.Logging.AddFilter("OutThink", LogLevel.Debug);
builder.Services.AddApplicationInsightsTelemetry();

// Register services
builder.Services.AddHttpClient<HttpRequestService>();
builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();
builder.Services.AddSingleton<IHttpRequestService, HttpRequestService>();
builder.Services.AddSingleton<ILoggingService, LoggingService>();
builder.Services.AddSingleton<IMessageProcessorService, MessageProcessorService>();
builder.Services.AddSingleton<IGraphApiClient, GraphApiClient>();
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

var config = app.Services.GetRequiredService<IConfigurationService>();
await config.ReloadAsync(); 

var startTime = DateTime.UtcNow;

// Minimal endpoints
app.MapGet("/", () => Results.Text("OK"));
app.MapGet("/status", () => Results.Text($"Worker running. Started at {startTime:O}"));

app.Run();
