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


// using Microsoft.AspNetCore.Builder;
// using Microsoft.AspNetCore.Hosting;
// using Microsoft.AspNetCore.Http;
// using OutThink.EmailInjectorApp.Services;
// using OutThink.EmailInjectorApp.Workers;
//
// namespace OutThink.EmailInjectorApp
// {
//     public class Program
//     {
//         
//         private static readonly DateTime StartTime = DateTime.UtcNow;
//         public static void Main(string[] args)
//         {
//             CreateHostBuilder(args).Build().Run();
//         }
//
//         public static IHostBuilder CreateHostBuilder(string[] args) =>
//             Host.CreateDefaultBuilder(args)
//                 .ConfigureAppConfiguration((hostingContext, config) =>
//                 {
//                     config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
//                           .AddUserSecrets<Program>()
//                           .AddEnvironmentVariables();
//                 })
//                 .ConfigureWebHostDefaults(webBuilder =>
//                 {
//                     webBuilder.ConfigureKestrel(serverOptions =>
//                     {
//                         serverOptions.ListenAnyIP(8080);
//                     });
//
//                     webBuilder.Configure(app =>
//                     {
//                         app.Run(async context =>
//                         {
//                             if (context.Request.Path == "/")
//                             {
//                                 await context.Response.WriteAsync($"OK");
//                             }
//                             else if (context.Request.Path == "/status")
//                             {
//                                 await context.Response.WriteAsync($"Worker running. Started at {StartTime:O}");
//                             }
//                             else
//                             {
//                                 context.Response.StatusCode = 404;
//                             }
//                         });
//                     });
//                 })
//                 .ConfigureServices((hostContext, services) =>
//                 {
//                     services.AddHttpClient<HttpRequestService>();
//                     services.AddSingleton<ConfigurationService>();
//                     services.AddSingleton<HttpRequestService>();
//                     services.AddSingleton<LoggingService>();
//                     services.AddSingleton<EmailInjectionService>();
//                     services.AddHostedService<Worker>();
//                 });
//     }
// }
