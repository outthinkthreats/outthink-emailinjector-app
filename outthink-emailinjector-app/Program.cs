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
