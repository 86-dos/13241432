# C# GeopolitRP Telegram Bot

Полнофункциональный телеграм-бот для геополитического RP с внутренним ИИ, механикой бюджета, памятью и самовосстановлением.

## Требования

- .NET 8 SDK
- Telegram Bot API токен (от @BotFather)
- (Опционально) OpenAI API ключ для ИИ-ответов

## Установка

```bash
cd csharp-bot
dotnet restore
dotnet build -c Release
```

## Конфигурация

Отредактируйте `appsettings.json`:

```json
{
  "TelegramToken": "YOUR_BOT_TOKEN",
  "OpenAIApiKey": "YOUR_OPENAI_KEY",
  "AdminIds": [123456789],
  "DatabasePath": "georp_csharp.db"
}
```

Или используйте переменные окружения:

```bash
export TelegramToken="YOUR_BOT_TOKEN"
export OpenAIApiKey="YOUR_OPENAI_KEY"
export AdminIds__0=123456789
```

## Запуск

### Локально

```bash
dotnet run --project CSharpGeoRP.csproj
```

### Docker

```bash
docker build -t georp-bot .
docker run -e TelegramToken="YOUR_TOKEN" georp-bot
```

## Команды

### Базовые
- `/start` — информация и начало
- `/register <ник> <страна>` — зарегистрировать страну и ник
- `/profile` — показать ник, страну и баланс
- `/leave` — выйти из страны (баланс обнуляется)

### Действия
- `/act <действие>` — потратить бюджет на действие (генерирует RP-ответ)
- `/prices` — показать цены на действия

### Память и история
- `/remember <заметка>` — сохранить заметку
- `/memory` — показать последние заметки
- `/history` — показать последние действия

### Администратору
- `/addbudget <telegram_id|страна|ник> <сумма>` — выдать средства игроку
- `/help` — полный список команд

## Механика

**Система бюджета:**
- Игрок регистрирует страну и получает баланс 0
- Админ выдаёт средства командой `/addbudget`
- Игрок тратит деньги командой `/act` на различные действия
- Каждое действие генерирует RP-ответ (через OpenAI или fallback шаблоны)

**Самовосстановление:**
- При ошибке бот логирует её в БД и файл `selfrepair.log`
- Если задан OpenAI ключ, генерирует предложение по исправлению
- Админ может просмотреть необработанные ошибки

**Интеграция ИИ:**
- Когда стоит OpenAI ключ, `/act` команда генерирует реалистичный RP-ответ
- Бот учитывает память игрока при генерации
- Без ключа используются встроенные fallback-шаблоны

## Развёртывание на бесплатный хостинг

### Replit

1. Создайте новый Replit проект (Python → выберите Dockerfile)
2. Загрузите содержимое папки `csharp-bot`
3. Добавьте Secrets: `TelegramToken`, `OpenAIApiKey` (опционально), `AdminIds`
4. Нажмите Run

### Oracle Cloud (Always Free)

```bash
# На VM
git clone <your-repo>
cd csharp-bot
sudo apt-get install -y dotnet-sdk-8.0
dotnet publish -c Release -o /opt/georp
cd /opt/georp

# Создайте systemd сервис
sudo tee /etc/systemd/system/georp-bot.service > /dev/null << EOF
[Unit]
Description=GeopolitRP Telegram Bot
After=network.target

[Service]
Type=simple
User=ubuntu
WorkingDirectory=/opt/georp
ExecStart=/usr/bin/dotnet /opt/georp/CSharpGeoRP.dll
Restart=always
RestartSec=10
Environment="TelegramToken=YOUR_TOKEN"

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl enable georp-bot
sudo systemctl start georp-bot
sudo systemctl status georp-bot
```

### Railway / Render

1. Push код в GitHub
2. Создайте новый проект, выберите репозиторий
3. Задайте переменные окружения (TelegramToken, OpenAIApiKey, AdminIds)
4. Нажмите Deploy

## Архитектура

- **Program.cs** — DI конфигурация и запуск хоста
- **Services/BotWorker.cs** — HostedService для опроса Telegram
- **Services/CommandHandler.cs** — обработка команд и сообщений
- **Services/Database.cs** — SQLite с Dapper (таблицы: users, history, memories, error_logs)
- **Services/AIService.cs** — генерация RP-ответов через OpenAI / fallback
- **Services/SelfRepairService.cs** — логирование ошибок и автоисправление

## Ошибки и логирование

Все ошибки логируются:
- Консоль (INFO+)
- БД (error_logs)
- Файл (selfrepair.log)

При наличии OpenAI ключа генерируются suggestions по исправлению.

## Лицензия

MIT

