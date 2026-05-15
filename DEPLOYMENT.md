# Развёртывание на бесплатный хостинг

## 1. Replit

### Шаги:

1. Зайдите на https://replit.com
2. Нажмите "Create" → "Import from GitHub" → вставьте ссылку на репо (или создайте новый C# проект)
3. В левой панели нажмите "Secrets" (🔒 иконка)
4. Добавьте переменные:
   - `TELEGRAM_TOKEN` = `YOUR_BOT_TOKEN`
   - `OPENAI_API_KEY` = (опционально)
   - `AdminIds__0` = `YOUR_ADMIN_ID`
5. Нажмите "Run" вверху

**Проблема:** Replit может останавливать процессы через 1 час. Решение:
- Используйте Uptime Robot (https://uptimerobot.com) для пингования каждые 5 минут
- Webhook: `https://your-replit-url.repl.co/ping`

---

## 2. Oracle Cloud (Always Free) — РЕКОМЕНДУЕТСЯ

Даёт бесплатный VM на неограниченный срок (если не превышать трафик).

### Шаги:

1. Создайте аккаунт на https://www.oracle.com/cloud/free/
2. Подтвердите платёжную карту (не будут списывать)
3. Перейдите в Console → Compute → Instances → Create Instance
4. Выберите:
   - Image: Ubuntu 22.04 (в free tier)
   - Shape: Ampere (ARM) — бесплатно
   - Public IP: Yes
   - SSH Key: загрузите свой или сгенерируйте новый
5. Создайте инстанс

### Развёртывание на Oracle VM:

```bash
# Подключитесь по SSH
ssh ubuntu@YOUR_PUBLIC_IP

# Установите .NET SDK
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --version 8.0
export PATH=$PATH:$HOME/.dotnet

# Клонируйте репо
git clone https://github.com/YOUR_USER/csharp-georp.git
cd csharp-georp/csharp-bot

# Конфигурируйте
cp .env.example .env
# Отредактируйте .env через nano
nano .env

# Соберите и опубликуйте
dotnet publish -c Release -o /opt/georp

# Создайте systemd сервис
sudo tee /etc/systemd/system/georp-bot.service > /dev/null << EOF
[Unit]
Description=GeopolitRP Telegram Bot
After=network.target

[Service]
Type=simple
User=ubuntu
WorkingDirectory=/opt/georp
ExecStart=$HOME/.dotnet/dotnet /opt/georp/CSharpGeoRP.dll
Restart=always
RestartSec=10
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
EOF

# Запустите сервис
sudo systemctl daemon-reload
sudo systemctl enable georp-bot
sudo systemctl start georp-bot

# Проверьте статус
sudo systemctl status georp-bot

# Смотрите логи
sudo journalctl -u georp-bot -f
```

### Открыть порты (если нужны):

```bash
sudo iptables -I INPUT -p tcp --dport 8080 -j ACCEPT
```

---

## 3. Railway

Простой деплой через GitHub.

### Шаги:

1. Загрузите код на GitHub
2. Зайдите на https://railway.app
3. Нажмите "New Project" → "Deploy from GitHub repo"
4. Выберите свой репо
5. Автоматически определит .NET проект
6. Перейдите в Project Settings → Variables
7. Добавьте:
   - `TelegramToken`
   - `OpenAIApiKey`
   - `AdminIds__0`
8. Нажмите Deploy

Бесплатный tier: 500 часов в месяц (достаточно для 24/7 бота).

---

## 4. Render

Аналог Railway.

### Шаги:

1. https://render.com → New → Web Service
2. Выберите GitHub репо
3. Name: `georp-bot`
4. Environment: `Docker`
5. Build & Deploy
6. В Environment укажите переменные

---

## 5. Docker Hub + любой VPS

Если у вас есть VPS на DigitalOcean, Linode, Hetzner и т.д.:

```bash
# На своей машине
docker build -t your-username/georp-bot:latest .
docker login
docker push your-username/georp-bot:latest

# На VPS
docker run -d \
  -e TelegramToken="YOUR_TOKEN" \
  -e AdminIds__0="YOUR_ID" \
  --name georp-bot \
  --restart unless-stopped \
  your-username/georp-bot:latest
```

---

## Сравнение хостингов

| Хостинг | Цена | Аптайм | Сложность | Рекомендуется |
|---------|------|--------|----------|--------------|
| Replit | Бесплатно | 1 час | Очень просто | Только для теста |
| Oracle Cloud | Бесплатно | 24/7 | Средняя | ✅ ЛУЧШЕ |
| Railway | Бесплатно (500ч) | 24/7 | Очень просто | Хорошо |
| Render | Бесплатно | 24/7 | Просто | Хорошо |
| VPS | $5-10/мес | 24/7 | Средняя | Если хотите контроль |

---

## Проверка, что бот работает

```bash
# Используйте curl или Postman
# Отправьте тестовое сообщение боту в Telegram
# Смотрите логи через:

# На хостинге с systemd:
sudo journalctl -u georp-bot -f

# В Docker:
docker logs -f georp-bot

# На Replit/Railway:
Смотрите вкладку Logs
```

## Проблемы при деплое

**Проблема:** `Could not find matching framework`
**Решение:** Убедитесь, что .NET 8 Runtime установлен на хосте

**Проблема:** Бот не отвечает
**Решение:** Проверьте токен, смотрите логи

**Проблема:** БД читается только
**Решение:** На хостинге может быть read-only файловая система; используйте переменные окружения для пути к БД или in-memory DB

**Проблема:** Нет OpenAI ответов
**Решение:** Это нормально! Используются fallback шаблоны. Если нужны ИИ ответы, добавьте OpenAI ключ.

---

## Локальный тест перед деплоем

```bash
cd csharp-bot
dotnet build -c Release
dotnet run --project CSharpGeoRP.csproj

# В Telegram найдите бота и отправьте /start
# Если видите ответ — всё готово к деплою
```
