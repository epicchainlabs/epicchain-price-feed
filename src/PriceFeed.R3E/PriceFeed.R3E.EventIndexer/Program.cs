using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PriceFeed.R3E.EventIndexer.Data;
using PriceFeed.R3E.EventIndexer.Services;

namespace PriceFeed.R3E.EventIndexer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🔍 R3E PriceFeed Event Indexer");
            Console.WriteLine("==============================");

            var host = CreateHostBuilder(args).Build();

            // Ensure database is created
            using (var scope = host.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<EventIndexerContext>();
                await context.Database.EnsureCreatedAsync();
            }

            await host.RunAsync();
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);
                    config.AddEnvironmentVariables();
                    config.AddCommandLine(args);
                })
                .ConfigureServices((context, services) =>
                {
                    // Add Entity Framework
                    var connectionString = context.Configuration.GetConnectionString("DefaultConnection") 
                        ?? "Data Source=events.db";
                    
                    services.AddDbContext<EventIndexerContext>(options =>
                        options.UseSqlite(connectionString));

                    // Add the indexer service
                    services.AddHostedService<EventIndexerService>();
                })
                .ConfigureLogging((context, logging) =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Information);
                });
    }
}