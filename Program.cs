using OutThink.EmailInjectorApp.Services;
using OutThink.EmailInjectorApp.Workers;

namespace OutThink.EmailInjectorApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                        .AddUserSecrets<Program>()
                        .AddEnvironmentVariables();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHttpClient<HttpRequestService>();
                    services.AddSingleton<ConfigurationService>();
                    services.AddSingleton<HttpRequestService>();
                    services.AddSingleton<LoggingService>();
                    services.AddSingleton<EmailInjectionService>();
                    services.AddHostedService<Worker>();
                });
    }
}
