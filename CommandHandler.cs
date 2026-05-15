using Telegram.Bot;
using Telegram.Bot.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Services;

public class CommandHandler
{
    private readonly ILogger<CommandHandler> _logger;
    private readonly IConfiguration _config;
    private readonly Database _db;
    private readonly AIService _ai;
    private List<long> _adminIds;

    private readonly Dictionary<string, int> _priceMap = new()
    {
        { "дипломатия", 500 },
        { "торговля", 300 },
        { "санкции", 1000 },
        { "союз", 800 },
        { "война", 2000 },
        { "разведка", 1200 },
    };

    public CommandHandler(ILogger<CommandHandler> logger, IConfiguration config, Database db, AIService ai)
    {
        _logger = logger;
        _config = config;
        _db = db;
        _ai = ai;
        _adminIds = config.GetSection("AdminIds").Get<List<long>>() ?? new();
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Telegram.Bot.Types.Update update, CancellationToken ct)
    {
        try
        {
            if (update.Message?.Text is null) return;
            var msg = update.Message;
            var text = msg.Text;
            var chatId = msg.Chat.Id;
            var userId = msg.From?.Id ?? 0;

            switch (true)
            {
                case text.StartsWith("/start"):
                    await HandleStart(botClient, chatId);
                    break;
                case text.StartsWith("/register"):
                    await HandleRegister(botClient, chatId, userId, text);
                    break;
                case text.StartsWith("/profile"):
                    await HandleProfile(botClient, chatId, userId);
                    break;
                case text.StartsWith("/leave"):
                    await HandleLeave(botClient, chatId, userId);
                    break;
                case text.StartsWith("/act"):
                    await HandleAct(botClient, chatId, userId, text);
                    break;
                case text.StartsWith("/addbudget"):
                    await HandleAddBudget(botClient, chatId, userId, text);
                    break;
                case text.StartsWith("/remember"):
                    await HandleRemember(botClient, chatId, userId, text);
                    break;
                case text.StartsWith("/memory"):
                    await HandleMemory(botClient, chatId, userId);
                    break;
                case text.StartsWith("/history"):
                    await HandleHistory(botClient, chatId, userId);
                    break;
                case text.StartsWith("/prices"):
                    await HandlePrices(botClient, chatId);
                    break;
                case text.StartsWith("/help"):
                    await HandleHelp(botClient, chatId);
                    break;
                default:
                    await botClient.SendTextMessageAsync(chatId, "Команда не распознана. Используйте /help.");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling update");
            _db.LogError(ex.Message, ex.StackTrace ?? "", null);
        }
    }

    private async Task HandleStart(ITelegramBotClient bot, long chatId)
    {
        var text = "Добро пожаловать в GeopolitRP!\n\n"
                 + "Зарегистрируйте страну и ник: /register <ник> <страна>\n"
                 + "Пример: /register Solaris Солнечная Федерация\n\n"
                 + "Используйте /help для всех команд.";
        await bot.SendTextMessageAsync(chatId, text);
    }

    private async Task HandleRegister(ITelegramBotClient bot, long chatId, long userId, string text)
    {
        var parts = text.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            await bot.SendTextMessageAsync(chatId, "Использование: /register <ник> <страна>\nПример: /register Solaris Солнечная Федерация");
            return;
        }

        var existing = _db.GetUser(userId);
        if (existing?.Country is not null)
        {
            await bot.SendTextMessageAsync(chatId, $"Вы уже зарегистрированы как {existing.Nickname} в {existing.Country}. Используйте /leave чтобы выйти.");
            return;
        }

        var nick = parts[1];
        var country = string.Join(" ", parts.Skip(2));
        
        if (nick.Length > 20 || country.Length > 50)
        {
            await bot.SendTextMessageAsync(chatId, "Ник и страна должны быть короче.");
            return;
        }

        _db.CreateOrUpdateUser(userId, nick, country);
        await bot.SendTextMessageAsync(chatId, $"✓ Зарегистрировано: {nick} из {country}.\nБюджет: 0. Админ добавит средства через /addbudget.");
    }

    private async Task HandleProfile(ITelegramBotClient bot, long chatId, long userId)
    {
        var u = _db.GetUser(userId);
        if (u?.Country is null)
        {
            await bot.SendTextMessageAsync(chatId, "Вы не зарегистрированы. Используйте /register <ник> <страна>");
            return;
        }

        var bal = FormatAmount(u.Balance);
        await bot.SendTextMessageAsync(chatId, $"📋 Ник: {u.Nickname}\n🌍 Страна: {u.Country}\n💰 Бюджет: {bal}");
    }

    private async Task HandleLeave(ITelegramBotClient bot, long chatId, long userId)
    {
        var u = _db.GetUser(userId);
        if (u?.Country is null)
        {
            await bot.SendTextMessageAsync(chatId, "Вы не состоите ни в какой стране.");
            return;
        }

        _db.LeaveCountry(userId);
        await bot.SendTextMessageAsync(chatId, $"👋 Вы покинули страну {u.Country}. Ваш баланс обнулён.\nЧтобы зарегистрировать новую страну: /register <ник> <страна>");
    }

    private async Task HandleAct(ITelegramBotClient bot, long chatId, long userId, string text)
    {
        var u = _db.GetUser(userId);
        if (u?.Country is null)
        {
            await bot.SendTextMessageAsync(chatId, "Вы не зарегистрированы. Используйте /start");
            return;
        }

        var action = text.Replace("/act", "").Trim();
        if (string.IsNullOrWhiteSpace(action))
        {
            await bot.SendTextMessageAsync(chatId, "Укажите действие: /act <действие>\nПример: /act вложиться в инфраструктуру");
            return;
        }

        var cost = EstimateCost(action);
        if (u.Balance < cost)
        {
            var need = FormatAmount(cost);
            var have = FormatAmount(u.Balance);
            await bot.SendTextMessageAsync(chatId, $"❌ Недостаточно средств.\nНужно: {need}, у вас: {have}");
            return;
        }

        _db.UpdateBalance(userId, -cost);
        var result = await _ai.GenerateResponse(action, u.Country, _db.GetMemorySummary(userId));
        _db.AddHistory(userId, action, cost, result);

        // Auto-remember key events
        var keywords = new[] { "союз", "соглаш", "переговор", "альянс", "кризис", "санкц", "война", "шпион" };
        if (keywords.Any(k => action.ToLower().Contains(k)))
        {
            _db.RememberNote(userId, $"{action} → {result}");
        }

        var newBal = FormatAmount(_db.GetUser(userId)?.Balance ?? 0);
        await bot.SendTextMessageAsync(chatId, $"{result}\n\n💸 Стоимость: {FormatAmount(cost)}\n💰 Баланс: {newBal}");
    }

    private async Task HandleAddBudget(ITelegramBotClient bot, long chatId, long userId, string text)
    {
        if (!_adminIds.Contains(userId))
        {
            await bot.SendTextMessageAsync(chatId, "Только администратор может выдавать бюджет.");
            return;
        }

        var parts = text.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            await bot.SendTextMessageAsync(chatId, "Использование: /addbudget <telegram_id|страна|ник> <сумма>\nПример: /addbudget 123456789 5000");
            return;
        }

        var target = parts[1];
        if (!int.TryParse(parts[2].Replace(" ", ""), out var amount) || amount <= 0)
        {
            await bot.SendTextMessageAsync(chatId, "Некорректная сумма.");
            return;
        }

        var targetUser = long.TryParse(target, out var tgId) ? _db.GetUser(tgId) : _db.GetUserByCountry(target);
        if (targetUser is null)
        {
            await bot.SendTextMessageAsync(chatId, "Пользователь не найден.");
            return;
        }

        _db.UpdateBalance(targetUser.TelegramId, amount);
        var newBal = FormatAmount(_db.GetUser(targetUser.TelegramId)?.Balance ?? 0);
        await bot.SendTextMessageAsync(chatId, $"✓ Выдано {FormatAmount(amount)} для {targetUser.Nickname}. Новый баланс: {newBal}");
    }

    private async Task HandleRemember(ITelegramBotClient bot, long chatId, long userId, string text)
    {
        var note = text.Replace("/remember", "").Trim();
        if (string.IsNullOrWhiteSpace(note))
        {
            await bot.SendTextMessageAsync(chatId, "Использование: /remember <заметка>");
            return;
        }

        _db.RememberNote(userId, note);
        await bot.SendTextMessageAsync(chatId, "✓ Заметка сохранена.");
    }

    private async Task HandleMemory(ITelegramBotClient bot, long chatId, long userId)
    {
        var notes = _db.GetMemories(userId, 5);
        if (!notes.Any())
        {
            await bot.SendTextMessageAsync(chatId, "Нет сохранённых заметок.");
            return;
        }

        var txt = "📚 Память:\n" + string.Join("\n", notes.Select(n => $"- {n.Note}"));
        await bot.SendTextMessageAsync(chatId, txt);
    }

    private async Task HandleHistory(ITelegramBotClient bot, long chatId, long userId)
    {
        var history = _db.GetHistory(userId, 5);
        if (!history.Any())
        {
            await bot.SendTextMessageAsync(chatId, "Нет истории действий.");
            return;
        }

        var lines = history.Select(h => $"- {h.Action} ({FormatAmount(h.Cost)})");
        var txt = "📖 История:\n" + string.Join("\n", lines);
        await bot.SendTextMessageAsync(chatId, txt);
    }

    private async Task HandlePrices(ITelegramBotClient bot, long chatId)
    {
        var lines = _priceMap.Select(kv => $"- {kv.Key}: {FormatAmount(kv.Value)}");
        var txt = "💵 Цены на действия:\n" + string.Join("\n", lines);
        await bot.SendTextMessageAsync(chatId, txt);
    }

    private async Task HandleHelp(ITelegramBotClient bot, long chatId)
    {
        var help = "/start — начало\n"
                 + "/register <ник> <страна> — зарегистрировать страну\n"
                 + "/profile — ник, страна, баланс\n"
                 + "/leave — выйти из страны\n"
                 + "/act <действие> — потратить бюджет\n"
                 + "/remember <заметка> — сохранить заметку\n"
                 + "/memory — показать заметки\n"
                 + "/history — показать действия\n"
                 + "/prices — цены на действия\n"
                 + "/help — этот текст";
        await bot.SendTextMessageAsync(chatId, help);
    }

    private int EstimateCost(string action)
    {
        var lower = action.ToLower();
        foreach (var (key, price) in _priceMap)
        {
            if (lower.Contains(key)) return price;
        }
        return 100; // default cost
    }

    private string FormatAmount(int amount) => $"{amount:N0}".Replace(",", " ");
}
