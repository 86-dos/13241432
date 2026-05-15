# ❓ FAQ и Troubleshooting

## Частые вопросы

### Установка и сборка

**Q: Как установить .NET 8 SDK?**

A: Перейдите на https://dotnet.microsoft.com/download и выберите вашу ОС.

Или через пакетный менеджер:
```bash
# Linux (Ubuntu/Debian)
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --version 8.0

# macOS
brew install dotnet@8

# Windows (Chocolatey)
choco install dotnet
```

**Q: Как проверить, что .NET установлен?**

A:
```bash
dotnet --version
dotnet --info
```

**Q: Ошибка: "Could not find matching framework"**

A: Убедитесь, что .NET 8 Runtime установлен:
```bash
dotnet --list-runtimes
```

---

### Конфигурация

**Q: Где взять Telegram токен?**

A:
1. Найдите @BotFather в Telegram
2. Отправьте `/start`
3. Отправьте `/newbot`
4. Выполните инструкции
5. Скопируйте токен

**Q: Как задать токен в приложении?**

A: Три способа (в порядке приоритета):

1. **Переменная окружения (лучше для Docker):**
```bash
export TelegramToken="YOUR_TOKEN"
```

2. **appsettings.json (для локального запуска):**
```json
{
  "TelegramToken": "YOUR_TOKEN"
}
```

3. **.env файл (конвенция):**
```bash
cp .env.example .env
# Отредактируйте .env
```

**Q: Как добавить себя админом?**

A: В `appsettings.json`:
```json
{
  "AdminIds": [123456789, 987654321]
}
```

Узнайте свой ID: @userinfobot в Telegram

**Q: Как получить OpenAI ключ?**

A:
1. Зайдите на https://platform.openai.com
2. Sign up или Login
3. Перейдите в Account → API keys
4. Create new secret key
5. Скопируйте и добавьте в конфиг

---

### Запуск

**Q: Как запустить локально?**

A:
```bash
cd csharp-bot
dotnet restore
dotnet run
```

**Q: Как запустить в Docker?**

A:
```bash
docker build -t georp-bot .
docker run -e TelegramToken="YOUR_TOKEN" georp-bot
```

Или просто:
```bash
docker-compose up -d
```

**Q: Процесс зависает после "Started receiving"**

A: Это нормально! Бот ждёт сообщений. Отправьте боту сообщение в Telegram и увидите логи.

**Q: Как остановить бота?**

A:
- Локально: `Ctrl+C`
- Docker: `docker stop container_id`
- Docker Compose: `docker-compose down`

---

### БД и хранение

**Q: Где хранится БД?**

A: По умолчанию в `georp_csharp.db` в текущей папке.

Можно изменить в `appsettings.json`:
```json
{
  "DatabasePath": "/path/to/custom.db"
}
```

**Q: Как подключиться к БД напрямую?**

A: Используйте SQLite браузер (DB Browser for SQLite):
```bash
# Ubuntu
sudo apt install sqlitebrowser
sqlitebrowser georp_csharp.db
```

Или из терминала:
```bash
sqlite3 georp_csharp.db
sqlite> SELECT * FROM users;
```

**Q: Как экспортировать данные?**

A:
```bash
# CSV
sqlite3 georp_csharp.db ".mode csv" ".output users.csv" "SELECT * FROM users;"

# JSON (через Python)
python3 -c "import sqlite3, json; print(json.dumps(sqlite3.connect('georp_csharp.db').execute('SELECT * FROM users').fetchall()))"
```

---

### ИИ и OpenAI

**Q: Будет ли работать без OpenAI ключа?**

A: ✅ Да! Используются встроенные fallback шаблоны. ИИ добавляет реалистичности, но не обязателен.

**Q: Как проверить, что OpenAI работает?**

A:
1. Добавьте ключ в `appsettings.json`
2. Запустите бота
3. В Telegram отправьте: `/act что-то`
4. Смотрите логи — если "OpenAI call failed" — не работает

**Q: OpenAI стоит денег?**

A: Да, но дешево. Free trial даёт $18 на тестирование. Для одного бота с 10 пользователями ~$0-1/месяц.

**Q: Как отключить OpenAI?**

A: Оставьте `OpenAIApiKey` пустым или удалите переменную.

---

### Ошибки при запуске

**Ошибка: "Telegram token not set"**

Решение:
```bash
export TelegramToken="YOUR_ACTUAL_TOKEN"
# Проверьте, что это правильный токен от @BotFather
```

**Ошибка: "Could not resolve service"**

Решение: В Program.cs забыли зарегистрировать Service:
```csharp
services.AddSingleton<Services.YourNewService>();
```

**Ошибка: "SqliteException: database is locked"**

Решение: Закройте все соединения с БД. Может быть, открыта в DB Browser?

**Ошибка: "ApiRequestException: Unauthorized"**

Решение: Неверный токен. Проверьте в @BotFather.

---

### Логирование и отладка

**Q: Где смотреть логи?**

A:
- Консоль — прямо в терминале
- Файл — `selfrepair.log` (ошибки)
- БД — таблица `error_logs`
- Docker — `docker logs container_id`

**Q: Как увеличить verbosity логирования?**

A: В `appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Debug"
    }
  }
}
```

**Q: Как отключить логирование?**

A:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Critical"
    }
  }
}
```

---

### Деплой на хостинги

**Q: Какой хостинг выбрать?**

A:
- **Тестирование** → Replit
- **Production** → Oracle Cloud (бесплатно, 24/7) или Railway (просто)

**Q: Почему Replit стопится?**

A: Бесплатный tier ограничен 1 часом. Используйте Uptime Robot для пингования.

**Q: Как запустить на Oracle Cloud?**

A: Полная инструкция в `DEPLOYMENT.md` (20+ шагов с примерами).

**Q: Сколько стоит Railway?**

A: ~$5/месяц после бесплатных 500 часов.

---

### Производительность

**Q: Может ли бот обслужить 1000 пользователей?**

A: ✅ Легко! C# async архитектура справляется с 5000+ одновременных соединений.

**Q: Много ли памяти потребляет?**

A: ~300 MB для базового случая. Растёт медленно с числом пользователей.

**Q: Как оптимизировать?**

A:
- Используйте Connection Pooling в SQLite
- Кешируйте часто используемые данные
- Используйте асинхронные операции везде

---

### Обновления и версионирование

**Q: Как обновиться до новой версии?**

A:
```bash
git pull origin main
dotnet restore
dotnet build -c Release
```

**Q: Совместима ли нова версия с старой БД?**

A: ✅ Да, миграции данных происходят автоматически.

---

### Безопасность

**Q: Безопасно ли хранить токены в `.env`?**

A: **НЕТ!** Используйте:
- Переменные окружения в Docker/VPS
- Secret Manager (AWS, Azure, Kubernetes)
- Не коммитьте `.env` в Git

**Q: Как защитить админ команды?**

A: Проверка `AdminIds`:
```csharp
if (!_adminIds.Contains(userId))
    // Только админы могут это делать
```

**Q: Логируются ли токены?**

A: Нет, они вычищены из логов.

---

### Тестирование

**Q: Как писать тесты?**

A: Используйте xUnit:
```bash
dotnet add package xunit
dotnet new xunit -n CSharpGeoRP.Tests
dotnet test
```

**Q: Как мокировать Telegram?**

A: Используйте Moq:
```bash
dotnet add package Moq
```

---

### Многоязычность

**Q: Как добавить другой язык?**

A: Замените строки в `CommandHandler.cs`:
```csharp
// Русский
await bot.SendTextMessageAsync(chatId, "Зарегистрировано!");

// Английский
await bot.SendTextMessageAsync(chatId, "Registered!");
```

Или используйте resource files (.resx).

---

## Troubleshooting Таблица

| Проблема | Причина | Решение |
|----------|---------|---------|
| Бот не запускается | Токен не установлен | Проверьте `appsettings.json` |
| `OperationCancelledException` | Приложение завершилось | Нормально, перезапустите |
| БД заблокирована | Несколько соединений | Закройте DB Browser |
| Медленно обрабатывает | Много пользователей | Добавьте Connection Pooling |
| Нет ошибок в логах | Логирование отключено | Включите в `appsettings.json` |
| Docker не собирается | .NET SDK не установлен | Используйте Docker официальные образы |

---

## Если ничего не помогает

1. Проверьте все переменные окружения
2. Смотрите полные логи: `docker logs -f container`
3. Перезапустите полностью: `docker-compose down` → `up -d`
4. Очистите кеш: `docker system prune -a`
5. Пересоберите: `dotnet clean && dotnet build`

---

## Полезные команды

```bash
# Проверка статуса
dotnet run --help

# Очистка
dotnet clean

# Полная пересборка
dotnet clean && dotnet restore && dotnet build

# Запуск тестов
dotnet test

# Публикация для Docker
dotnet publish -c Release -o ./publish

# Docker команды
docker ps -a           # Список контейнеров
docker logs -f name    # Логи контейнера
docker exec name bash  # Зайти в контейнер
docker rm name         # Удалить контейнер
docker rmi name:tag    # Удалить образ

# SQLite команды
sqlite3 db.db "SELECT * FROM users;"
sqlite3 db.db ".tables"
sqlite3 db.db ".schema"
```

---

**Не нашли ответ?** Смотрите:
- README.md — полная документация
- DEPLOYMENT.md — инструкции развёртывания
- Логи приложения (консоль или файл)
- Таблица `error_logs` в БД

---

**Good luck! 🚀**
