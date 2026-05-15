# 🎮 GeopolitRP Telegram Bot — C# Версия

## ✅ Что реализовано

### Ядро бота
- ✅ **Telegram Polling** — опрос обновлений через Telegram.Bot API
- ✅ **Команды** — 11+ команд для игроков и админов
- ✅ **SQLite база данных** — с Dapper ORM
- ✅ **Dependency Injection** — через Microsoft.Extensions
- ✅ **Логирование** — консоль, БД, файл

### Игровая механика
- ✅ **/register** — регистрация страны и ника (переопустить баланс)
- ✅ **/profile** — показать профиль
- ✅ **/leave** — выйти из страны (баланс → 0)
- ✅ **/act** — потратить бюджет на действие (с RP ответом)
- ✅ **/addbudget** — админ выдаёт средства
- ✅ **/prices** — показать цены на действия
- ✅ **/remember** — сохранить заметку
- ✅ **/memory** — показать заметки
- ✅ **/history** — показать последние действия
- ✅ **/help** — список команд

### ИИ и самовосстановление
- ✅ **OpenAI интеграция** — генерация RP-ответов через ChatGPT
- ✅ **Fallback шаблоны** — работает без API ключа
- ✅ **Автологирование ошибок** — в БД и `selfrepair.log`
- ✅ **Автоисправление** — предложение fixes через ИИ
- ✅ **Обработка исключений** — try-catch везде

### Архитектура
- ✅ **BotWorker** — HostedService для фонового опроса
- ✅ **CommandHandler** — обработка сообщений
- ✅ **Database** — SQLite + Dapper с 4 таблицами
- ✅ **AIService** — OpenAI + fallback
- ✅ **SelfRepairService** — ошибки, логирование, исправления

### Развёртывание
- ✅ **Docker** — Dockerfile с многоэтапной сборкой
- ✅ **docker-compose** — для локального 24/7 запуска
- ✅ **DEPLOYMENT.md** — инструкции для Replit, Oracle Cloud, Railway
- ✅ **.env.example** — шаблон конфигурации
- ✅ **QUICKSTART.md** — быстрый старт на локальной машине

---

## 📁 Структура проекта

```
csharp-bot/
├── CSharpGeoRP.csproj          # Конфиг проекта (.NET 8)
├── Program.cs                  # DI, конфиг, старт хоста
├── Services/
│   ├── BotWorker.cs            # HostedService для опроса Telegram
│   ├── CommandHandler.cs       # Обработчик команд (11+ команд)
│   ├── Database.cs             # SQLite + Dapper (4 таблицы)
│   ├── AIService.cs            # OpenAI + fallback шаблоны
│   └── SelfRepairService.cs    # Логирование и автоисправление ошибок
├── appsettings.json            # Конфиг (токены, админы, БД путь)
├── Dockerfile                  # Многоэтапная сборка Docker
├── docker-compose.yml          # Docker Compose для локального запуска
├── .gitignore                  # Исключить bin, obj, db, logs
├── .env.example                # Шаблон переменных окружения
├── README.md                   # Полная документация
├── QUICKSTART.md               # Быстрый старт (локально + Docker)
├── DEPLOYMENT.md               # Деплой на бесплатные хостинги
└── Tests.cs                    # Примеры тестов (xUnit)
```

---

## 🚀 Быстрый старт

### Локально (Linux/Mac/Windows)

```bash
cd csharp-bot

# Установите .NET 8 SDK (если не установлен)
# https://dotnet.microsoft.com/download

# Соберите и запустите
dotnet restore
dotnet build -c Release
dotnet run --project CSharpGeoRP.csproj
```

### Docker

```bash
cd csharp-bot
docker-compose up -d
```

Или вручную:
```bash
docker build -t georp-bot .
docker run -e TelegramToken="YOUR_TOKEN" georp-bot
```

---

## ⚙️ Конфигурация

Заполните `appsettings.json` или установите переменные окружения:

```json
{
  "TelegramToken": "YOUR_TELEGRAM_BOT_TOKEN",
  "OpenAIApiKey": "YOUR_OPENAI_KEY",
  "AdminIds": [123456789],
  "DatabasePath": "georp_csharp.db"
}
```

Или через `.env`:

```bash
export TelegramToken="YOUR_TOKEN"
export OpenAIApiKey="YOUR_OPENAI_KEY"
export AdminIds__0=123456789
```

---

## 📊 База данных

4 таблицы SQLite:

| Таблица | Столбцы | Назначение |
|---------|---------|-----------|
| **users** | telegram_id, nickname, country, balance | Профили игроков |
| **history** | telegram_id, action, cost, result, created_at | История действий |
| **memories** | telegram_id, note, created_at | Заметки игрока |
| **error_logs** | error_message, stack_trace, proposed_fix, applied | Ошибки и исправления |

---

## 🤖 ИИ и самовосстановление

### Если стоит OpenAI ключ:
- `/act` команда генерирует реалистичный RP-ответ через ChatGPT
- Ошибки в коде → автоматически генерируется предложение по исправлению
- Логируется в `error_logs` таблицу

### Без OpenAI ключа:
- Используются встроенные fallback шаблоны (работает отлично)
- Ошибки логируются в `selfrepair.log`

---

## 🌐 Развёртывание на бесплатные хостинги

### Рекомендуется: Oracle Cloud (Always Free)

```bash
# Полная инструкция в DEPLOYMENT.md
# Бесплатный VM на неограниченный срок
```

### Альтернативы:
- **Railway** — 500 часов в месяц (достаточно)
- **Render** — постоянный аптайм
- **Replit** — просто, но стопится через 1 час

Полная инструкция: [DEPLOYMENT.md](DEPLOYMENT.md)

---

## 📝 Команды для админов

```bash
# Выдать 5000 денег игроку ID 123456789
/addbudget 123456789 5000

# Выдать денег по названию страны
/addbudget "Солнечная Федерация" 3000

# Выдать денег по нику
/addbudget Solaris 2000
```

---

## ✨ Особенности

✅ **Более предусмотрительный** чем Python версия:
- Валидация длины ника/страны
- Проверка прав админа перед выдачей бюджета
- Расширенная обработка ошибок везде
- Логирование всех действий
- Graceful shutdown

✅ **Умнее и умнее**:
- ИИ-генерация RP ответов
- Автоматическое запоминание ключевых событий
- Предложение исправлений при ошибках
- Fallback стратегии везде (никогда не упадёт)

✅ **Лучше архитектура**:
- Dependency Injection (тестируемо)
- Async/await везде
- Структурированное логирование
- Разделение ответственности (Services)

---

## 🧪 Тестирование

```bash
# Примеры тестов в Tests.cs
dotnet test
```

---

## 🐛 Проблемы?

1. **Бот не собирается** → Установите .NET 8 SDK
2. **Не отвечает** → Проверьте токен в `appsettings.json`
3. **No OpenAI** → Нормально! Используются fallback
4. **Сложно деплоить** → Используйте docker-compose или Railway

---

## 📖 Документация

- **README.md** — полная документация
- **QUICKSTART.md** — быстрый старт
- **DEPLOYMENT.md** — деплой на хостинги
- **Code comments** — в каждом Service

---

## 🎯 Следующие шаги

1. Заполните `appsettings.json` вашим Telegram токеном
2. Запустите локально: `dotnet run`
3. Протестируйте команды: `/start`, `/register`, `/profile`
4. Выберите хостинг и разверните (см. DEPLOYMENT.md)
5. Добавьте OpenAI ключ для ИИ (опционально)

---

## 📄 Лицензия

MIT

---

**Всё готово к использованию! 🚀**
