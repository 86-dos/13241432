using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Configuration;

namespace Services;

public class BotWorker : BackgroundService
{
    private readonly ILogger<BotWorker> _logger;
    private readonly IConfiguration _config;
    private readonly CommandHandler _handler;
    private readonly SelfRepairService _repair;
    private TelegramBotClient? _bot;

    public BotWorker(ILogger<BotWorker> logger, IConfiguration config, CommandHandler handler, SelfRepairService repair)
    {
        _logger = logger;
        _config = config;
        _handler = handler;
        _repair = repair;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        var token = _config["TelegramToken"];
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogCritical("Telegram token not set in configuration");
            throw new ArgumentException("TelegramToken not configured");
        }
        _bot = new TelegramBotClient(token);
        _logger.LogInformation("Bot client created");
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_bot == null) return;

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        _bot.StartReceiving(
            updateHandler: async (botClient, update, token) =>
            {
                try
                {
                    await _handler.HandleUpdateAsync(botClient, update, token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing update");
                    await _repair.HandleErrorAsync(ex);
                }
            },
            pollingErrorHandler: async (botClient, exception, token) =>
            {
                _logger.LogError(exception, "Polling error");
                await _repair.HandleErrorAsync(exception);
            },
            receiverOptions: receiverOptions,
            cancellationToken: stoppingToken
        );

        var me = await _bot.GetMeAsync(stoppingToken);
        _logger.LogInformation("Started receiving. Bot id: {BotId} name: {Name}", me.Id, me.FirstName);

        // keep running until cancelled
        await Task.Delay(-1, stoppingToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping bot");
        return base.StopAsync(cancellationToken);
    }
}
