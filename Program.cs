using OutThink.EmailInjectorApp.Clients;
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
builder.Services.AddSingleton<ConfigurationService>();
builder.Services.AddSingleton<HttpRequestService>();
builder.Services.AddSingleton<LoggingService>();
builder.Services.AddSingleton<MessageProcessorService>();
builder.Services.AddSingleton<GraphApiClient>();
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

var config = app.Services.GetRequiredService<ConfigurationService>();
await config.ReloadAsync(); 

var startTime = DateTime.UtcNow;

// Minimal endpoints
app.MapGet("/", () => Results.Text("OK"));
app.MapGet("/status", () => Results.Text($"Worker running. Started at {startTime:O}"));

app.Run();
