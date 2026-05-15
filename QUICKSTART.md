# Быстрый старт C# бота

## Локально (Linux/Mac/Windows)

### 1. Установите .NET 8 SDK

**Ubuntu/Debian:**
```bash
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --version 8.0
export PATH=$PATH:~/.dotnet
```

**macOS (Homebrew):**
```bash
brew install dotnet
```

**Windows:**
Скачайте с https://dotnet.microsoft.com/download

### 2. Клонируйте и соберите

```bash
cd csharp-bot
dotnet restore
dotnet build -c Release
```

### 3. Конфигурация

```bash
cp .env.example .env
# Отредактируйте .env и добавьте TELEGRAM_TOKEN
```

### 4. Запустите

```bash
dotnet run --project CSharpGeoRP.csproj
```

Если всё OK, увидите:
```
Started receiving. Bot id: 123456789 name: GeopolitRPBot
```

## Docker (рекомендуется для 24/7)

### 1. Соберите образ

```bash
docker build -t georp-bot:latest .
```

### 2. Запустите контейнер

```bash
docker run \
  -e TelegramToken="YOUR_BOT_TOKEN" \
  -e OpenAIApiKey="YOUR_OPENAI_KEY" \
  -e AdminIds__0="123456789" \
  -v georp-data:/app/data \
  --restart unless-stopped \
  georp-bot:latest
```

### 3. Или используйте docker-compose

```bash
cp .env.example .env
# Отредактируйте .env
docker-compose up -d
```

Проверьте логи:
```bash
docker-compose logs -f georp-bot
```

## Тестирование

После запуска найдите бота в Telegram и отправьте:
1. `/start` — должен вывести приветствие
2. `/register MyNick MyCountry` — зарегистрировать
3. `/profile` — показать профиль
4. `/help` — список команд

## Проблемы?

- **Ошибка сборки C#:** Убедитесь, что установлен .NET 8 SDK (`dotnet --version`)
- **Bot не отвечает:** Проверьте токен в `appsettings.json`
- **Нет OpenAI ответов:** Это нормально, используются fallback шаблоны
- **Логи:** Смотрите `selfrepair.log` и логи контейнера

## Деплой на бесплатный хостинг

См. README.md раздел "Развёртывание на бесплатный хостинг"
