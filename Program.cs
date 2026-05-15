using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        cfg.AddEnvironmentVariables();
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddSingleton<Services.Database>();
        services.AddSingleton<Services.AIService>();
        services.AddSingleton<Services.SelfRepairService>();
        services.AddSingleton<Services.CommandHandler>();
        services.AddHttpClient();
        services.AddHostedService<Services.BotWorker>();
    })
    .ConfigureLogging(lb =>
    {
        lb.AddConsole();
        lb.AddFilter("Microsoft", LogLevel.Warning);
    })
    .Build();

await host.RunAsync();