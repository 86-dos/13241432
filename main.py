import os
import json
import sqlite3
import logging
import traceback
import subprocess
from datetime import datetime
from typing import Optional
import random
import time

from dotenv import load_dotenv

try:
    from telegram import Update, InlineKeyboardButton, InlineKeyboardMarkup
    from telegram.ext import ApplicationBuilder, ContextTypes, CommandHandler, MessageHandler, filters, CallbackQueryHandler
except Exception:
    raise RuntimeError("Please install python-telegram-bot from requirements.txt")

load_dotenv()

TELEGRAM_TOKEN = os.getenv("TELEGRAM_TOKEN")
OPENAI_API_KEY = os.getenv("OPENAI_API_KEY")
ADMIN_IDS = [int(x) for x in os.getenv("ADMIN_IDS", "").split(",") if x.strip().isdigit()]

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

DB_PATH = os.getenv("DB_PATH", "georp.db")

try:
    import openai
    openai_available = True
    if OPENAI_API_KEY:
        openai.api_key = OPENAI_API_KEY
except Exception:
    openai_available = False


def init_db():
    conn = sqlite3.connect(DB_PATH, check_same_thread=False)
    cur = conn.cursor()
    cur.execute(
        """
    CREATE TABLE IF NOT EXISTS users (
        id INTEGER PRIMARY KEY,
        telegram_id INTEGER UNIQUE,
        nickname TEXT,
        country TEXT,
        faction TEXT,
        balance INTEGER,
        history TEXT
    )
    """
    )
    cur.execute(
        """
    CREATE TABLE IF NOT EXISTS memories (
        id INTEGER PRIMARY KEY,
        telegram_id INTEGER,
        note TEXT,
        created_at INTEGER
    )
    """
    )
    conn.commit()
    ensure_columns(cur)
    conn.commit()
    return conn


def ensure_columns(cur):
    cur.execute("PRAGMA table_info(users)")
    columns = [row[1] for row in cur.fetchall()]
    if "nickname" not in columns:
        cur.execute("ALTER TABLE users ADD COLUMN nickname TEXT")
    if "country" not in columns:
        cur.execute("ALTER TABLE users ADD COLUMN country TEXT")
    if "faction" not in columns:
        cur.execute("ALTER TABLE users ADD COLUMN faction TEXT")



DB = init_db()


def get_user(telegram_id: int):
    cur = DB.cursor()
    cur.execute("SELECT id, telegram_id, nickname, country, faction, balance, history FROM users WHERE telegram_id = ?", (telegram_id,))
    row = cur.fetchone()
    if not row:
        return None
    id_, tg_id, nickname, country, faction, balance, history = row
    return {"id": id_, "telegram_id": tg_id, "nickname": nickname, "country": country, "faction": faction, "balance": balance, "history": json.loads(history or "[]")}


def get_user_by_country(country: str):
    cur = DB.cursor()
    cur.execute(
        "SELECT id, telegram_id, nickname, country, faction, balance, history FROM users WHERE lower(country) = lower(?) OR lower(nickname) = lower(?) LIMIT 1",
        (country, country),
    )
    row = cur.fetchone()
    if not row:
        return None
    id_, tg_id, nickname, country, faction, balance, history = row
    return {"id": id_, "telegram_id": tg_id, "nickname": nickname, "country": country, "faction": faction, "balance": balance, "history": json.loads(history or "[]")}


def create_user(telegram_id: int, nickname: str, country: str, starting: int = 1000):
    cur = DB.cursor()
    cur.execute("INSERT OR IGNORE INTO users (telegram_id, nickname, country, faction, balance, history) VALUES (?, ?, ?, ?, ?, ?)",
                (telegram_id, nickname, country, None, starting, json.dumps([])))
    DB.commit()
    return get_user(telegram_id)


def update_user_balance(telegram_id: int, delta: int):
    cur = DB.cursor()
    cur.execute("UPDATE users SET balance = balance + ? WHERE telegram_id = ?", (delta, telegram_id))
    DB.commit()
    return get_user(telegram_id)


def append_history(telegram_id: int, entry: dict):
    user = get_user(telegram_id)
    if not user:
        return
    history = user["history"]
    history.append(entry)
    cur = DB.cursor()
    cur.execute("UPDATE users SET history = ? WHERE telegram_id = ?", (json.dumps(history), telegram_id))
    DB.commit()


def remember_note(telegram_id: int, note: str):
    cur = DB.cursor()
    cur.execute("INSERT INTO memories (telegram_id, note, created_at) VALUES (?, ?, ?)",
                (telegram_id, note, int(time.time())))
    DB.commit()


def get_memories_for_user(telegram_id: int):
    cur = DB.cursor()
    cur.execute("SELECT note, created_at FROM memories WHERE telegram_id = ? ORDER BY created_at DESC LIMIT 10", (telegram_id,))
    rows = cur.fetchall()
    return [{"note": row[0], "created_at": row[1]} for row in rows]


def get_memory_summary(telegram_id: int):
    notes = get_memories_for_user(telegram_id)
    if not notes:
        return None
    return "\n".join([f"- {n['note']}" for n in notes[:5]])


def format_amount(amount: int) -> str:
    return f"{amount:,}".replace(",", " ")


def write_repair_log(message: str):
    with open("bot_selfrepair.log", "a", encoding="utf-8") as f:
        f.write(f"[{datetime.utcnow().isoformat()}] {message}\n")


def apply_patch_to_file(patch_text: str, file_path: str = "bot.py") -> bool:
    if not patch_text.strip():
        return False
    try:
        proc = subprocess.run(
            ["patch", file_path],
            input=patch_text,
            text=True,
            capture_output=True,
            check=False,
        )
        if proc.returncode == 0:
            write_repair_log("Patch applied successfully.")
            return True
        write_repair_log(f"Patch failed: {proc.stderr}")
    except FileNotFoundError:
        write_repair_log("patch command not found")
    except Exception as exc:
        write_repair_log(f"apply_patch error: {exc}")
    return False


def auto_repair_error(error: Exception, traceback_text: str) -> str:
    if not openai_available or not OPENAI_API_KEY:
        write_repair_log("OpenAI unavailable, cannot auto-repair.")
        return "OpenAI unavailable, error logged."
    prompt = (
        "Ты бот-ассистент, который исправляет ошибки Python в файле bot.py. "
        "Проанализируй трассировку и предложи минимальное исправление. "
        "Если нужно изменить файл, выведи только unified diff для bot.py. "
        "Если patch не требуется, верни текст 'NO_PATCH'.\n\n"
        "Traceback:\n" + traceback_text
    )
    try:
        resp = openai.ChatCompletion.create(
            model="gpt-3.5-turbo",
            messages=[
                {"role": "user", "content": prompt},
            ],
            max_tokens=700,
            temperature=0.2,
        )
        content = resp.choices[0].message.content.strip()
        write_repair_log(f"AI repair response:\n{content}")
        if content.startswith("NO_PATCH") or "NO_PATCH" in content:
            return "AI did not generate a patch. See bot_selfrepair.log."
        # Try to extract diff between lines starting with --- and ending with no more diff
        if "--- bot.py" in content or "***" in content or content.startswith("diff "):
            patch_text = content
            applied = apply_patch_to_file(patch_text)
            return "Patch applied." if applied else "Patch generated but not applied. See bot_selfrepair.log."
        return "AI response logged; no patch detected."
    except Exception as exc:
        write_repair_log(f"auto_repair_error failed: {exc}")
        return "Auto repair failed, logged."


async def error_handler(update: object, context: ContextTypes.DEFAULT_TYPE):
    error = context.error
    tb = "".join(traceback.format_exception(type(error), error, error.__traceback__))
    write_repair_log(f"Error: {tb}")
    if openai_available and OPENAI_API_KEY:
        repair_result = auto_repair_error(error, tb)
    else:
        repair_result = "Auto repair not available."
    logger.error("Handler error: %s", tb)
    logger.info("Self-repair result: %s", repair_result)
    if hasattr(update, "message") and update.message:
        try:
            await update.message.reply_text(
                "Произошла ошибка. Я попытался сам её исправить. Админ может просмотреть bot_selfrepair.log."
            )
        except Exception:
            pass


async def start(update: Update, context: ContextTypes.DEFAULT_TYPE):
    tg_id = update.effective_user.id
    user = get_user(tg_id)
    if user and user.get("country"):
        await update.message.reply_text(
            f"С возвращением, {update.effective_user.first_name}! Ник: {user['nickname']}. Страна: {user['country']}. Бюджет: {format_amount(user['balance'])}"
        )
        return
    await update.message.reply_text(
        "Добро пожаловать в GeopolitRP! Чтобы зарегистрировать страну и ник, отправьте команду:\n"
        "/register <ник> <страна>\n"
        "Пример: /register Solaris Солнечная Федерация"
    )
    await update.message.reply_text(
        "Добро пожаловать в GeopolitRP! Чтобы зарегистрировать страну и ник, отправьте команду:\n"
        "/register <ник> <страна>\n"
        "Пример: /register Solaris Солнечная Федерация"
    )


async def profile(update: Update, context: ContextTypes.DEFAULT_TYPE):
    tg_id = update.effective_user.id
    user = get_user(tg_id)
    if not user:
        await update.message.reply_text("Вы не зарегистрированы. Отправьте /start")
        return
    text = (
        f"Ник: {user['nickname']}\n"
        f"Страна: {user['country'] or 'нет'}\n"
        f"Бюджет: {format_amount(user['balance'])}"
    )
    await update.message.reply_text(text)


async def budget_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE):
    tg_id = update.effective_user.id
    user = get_user(tg_id)
    if not user:
        await update.message.reply_text("Вы не зарегистрированы. Отправьте /start")
        return
    text = (
        "Механика бюджета:\n"
        "- Зарегистрируйте страну и ник: /register <ник> <страна>\n"
        "- Покиньте страну: /leave\n"
        "- Админ выдаёт средства командой: /addbudget <telegram_id|страна|ник> <сумма>\n"
        "- Вы тратите бюджет через /act <действие>\n"
        "- Пример расхода: /act вложиться в инфраструктуру (пример суммы: 1 000)\n"
        "- Баланс отображается видами: 1 000, 10 000\n"
    )
    await update.message.reply_text(text)


ACTION_COSTS = {
    "инвестировать": 5000,
    "санкции": 3000,
    "провокация": 12000,
    "шпионаж": 8000,
    "мирная инициатива": 2500,
}


def estimate_cost(action_text: str) -> int:
    for k, v in ACTION_COSTS.items():
        if k in action_text.lower():
            return v
    return 2000


def internal_ai_response(prompt: str, system: Optional[str] = None, country: Optional[str] = None, memory: Optional[str] = None) -> str:
    # If OpenAI available, use it. Otherwise fallback to simple template-based generator.
    if openai_available and OPENAI_API_KEY:
        try:
            # Build richer context including faction and memory
            messages = []
            messages.append({"role": "system", "content": system or "You are a geopolitical RP engine. Respond concisely in Russian and keep roleplay tone."})
            if country:
                messages.append({"role": "system", "content": f"User country: {country}. Tailor responses to that country's strategy and identity."})
            if memory:
                messages.append({"role": "system", "content": f"Remember these facts about the user:\n{memory}"})
            messages.append({"role": "user", "content": prompt})
            resp = openai.ChatCompletion.create(
                model="gpt-3.5-turbo",
                messages=messages,
                max_tokens=400,
            )
            return resp.choices[0].message.content.strip()
        except Exception as e:
            logger.exception("OpenAI call failed, falling back: %s", e)

    # Fallback generator
    templates = [
        "{actor} предпринимает шаг: {action}. Это вызвало ответ от соседей — {effect}.",
        "Дипломатические каналы реагируют на {action} {actor} — результат: {effect}.",
        "{actor} использует ресурсы для {action}. В краткосрочной перспективе: {effect}.",
    ]
    effects = [
        "экономическое давление", "рост влияния", "обострение конфликтов", "усиление сотрудничества", "волатильность рынка",
    ]
    import random

    t = random.choice(templates)
    eff = random.choice(effects)
    actor = country or "Ваша страна"
    # small variation based on words
    if "санкц" in prompt.lower():
        eff = random.choice(["экономическое давление", "торговые ограничения"])
    return t.format(actor=actor, action=prompt, effect=eff)


async def register_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE):
    tg_id = update.effective_user.id
    existing = get_user(tg_id)
    if existing and existing.get("country"):
        await update.message.reply_text("Вы уже зарегистрированы. Используйте /profile, чтобы проверить баланс.")
        return
    if len(context.args) < 2:
        await update.message.reply_text("Использование: /register <ник> <страна>\nПример: /register Solaris Солнечная Федерация")
        return
    nickname = context.args[0]
    country = " ".join(context.args[1:]).strip()
    if existing:
        cur = DB.cursor()
        cur.execute("UPDATE users SET country = ?, nickname = ?, balance = 0 WHERE telegram_id = ?", (country, nickname, tg_id))
        DB.commit()
    else:
        create_user(tg_id, nickname=nickname, country=country, starting=0)
    await update.message.reply_text(f"Регистрация завершена: ник {nickname}, страна {country}. Бюджет 0. Админ добавит средства через /addbudget.")


async def act_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE):
    tg_id = update.effective_user.id
    user = get_user(tg_id)
    if not user:
        await update.message.reply_text("Вы не зарегистрированы. Отправьте /start")
        return
    text = " ".join(context.args) if context.args else (update.message.text or "").replace("/act", "").strip()
    if not text:
        await update.message.reply_text("Укажите действие: /act <действие>")
        return
    cost = estimate_cost(text)
    if user["balance"] < cost:
        await update.message.reply_text(f"Недостаточно средств. Требуется {format_amount(cost)}, у вас {format_amount(user['balance'])}")
        return
    # charge
    update_user_balance(tg_id, -cost)
    # generate AI response
    system_prompt = (
        "You are a roleplaying geopolitical engine. Отвечай по-русски в стиле RP, кратко, указывай последствия и учитывай бюджет."
    )
    memory = get_memory_summary(tg_id)
    ai_resp = internal_ai_response(text, system=system_prompt, country=user.get("country"), memory=memory)
    # append to history
    entry = {"action": text, "cost": cost, "result": ai_resp}
    append_history(tg_id, entry)
    # auto-save key events as memory
    if any(keyword in text.lower() for keyword in ["союз", "соглаш", "переговор", "альянс", "кризис", "санкц", "войн", "шпион"]):
        remember_note(tg_id, f"{text} -> {ai_resp}")
    await update.message.reply_text(
        f"{ai_resp}\n\nСтоимость: {format_amount(cost)}. Текущий баланс: {format_amount(get_user(tg_id)['balance'])}"
    )


async def addbudget_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE):
    if update.effective_user.id not in ADMIN_IDS:
        await update.message.reply_text("Только администратор может выдавать бюджет.")
        return
    if len(context.args) < 2:
        await update.message.reply_text("Использование: /addbudget <telegram_id|страна|ник> <сумма>\nПример: /addbudget 123456789 1 000")
        return
    target = context.args[0]
    amount_text = " ".join(context.args[1:]).replace(" ", "")
    if not amount_text.isdigit():
        await update.message.reply_text("Сумма должна быть числом, например 1000 или 1 000.")
        return
    amount = int(amount_text)
    if amount <= 0:
        await update.message.reply_text("Сумма должна быть больше нуля.")
        return
    user = None
    if target.isdigit():
        user = get_user(int(target))
    if not user:
        user = get_user_by_country(target)
    if not user:
        await update.message.reply_text("Страна, ник или пользователь не найдены.")
        return
    updated = update_user_balance(user["telegram_id"], amount)
    await update.message.reply_text(f"Добавлено {format_amount(amount)} бюджету {updated['country']} ({updated['nickname']}). Итоговый баланс: {format_amount(updated['balance'])}")


async def leave_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE):
    tg_id = update.effective_user.id
    user = get_user(tg_id)
    if not user or not user.get("country"):
        await update.message.reply_text("Вы не состоите ни в одной стране.")
        return
    cur = DB.cursor()
    cur.execute("UPDATE users SET country = NULL, balance = 0 WHERE telegram_id = ?", (tg_id,))
    DB.commit()
    await update.message.reply_text("Вы покинули страну. Ваш бюджет обнулён. Чтобы зарегистрировать новую страну, используйте /register <ник> <страна>.")
    tg_id = update.effective_user.id
    user = get_user(tg_id)
    if not user:
        await update.message.reply_text("Вы не зарегистрированы. Отправьте /start")
        return
    if not user.get("faction"):
        await update.message.reply_text("Вы не состоите ни в одной фракции.")
        return
    cur = DB.cursor()
    cur.execute("UPDATE users SET faction = NULL WHERE telegram_id = ?", (tg_id,))
    DB.commit()
    await update.message.reply_text("Вы вышли из фракции.")


async def prices_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE):
    lines = [f"{action.title()}: {format_amount(cost)}" for action, cost in ACTION_COSTS.items()]
    await update.message.reply_text("Цены на действия:\n" + "\n".join(lines))


async def transfer_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE):
    tg_id = update.effective_user.id
    user = get_user(tg_id)
    if not user:
        await update.message.reply_text("Вы не зарегистрированы. Отправьте /start")
        return
    if len(context.args) < 2:
        await update.message.reply_text("Использование: /transfer <telegram_id> <amount>")
        return
    try:
        target_id = int(context.args[0])
        amount = int(context.args[1])
    except ValueError:
        await update.message.reply_text("Неправильные параметры. ID и сумма должны быть числами.")
        return
    if amount <= 0:
        await update.message.reply_text("Сумма должна быть положительной.")
        return
    if user["balance"] < amount:
        await update.message.reply_text(f"Недостаточно средств. У вас {user['balance']}")
        return
    target = get_user(target_id)
    if not target:
        await update.message.reply_text("Получатель не найден в базе.")
        return
    update_user_balance(tg_id, -amount)
    updated_target = update_user_balance(target_id, amount)
    append_history(tg_id, {"action": f"transfer to {target_id}", "cost": amount, "result": f"sent to {updated_target['country']}"})
    append_history(target_id, {"action": f"transfer from {tg_id}", "cost": amount, "result": f"received from {user['country']}"})
    await update.message.reply_text(f"Перевод {format_amount(amount)} выполнен. Ваш баланс: {format_amount(get_user(tg_id)['balance'])}")


async def events_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE):
    # Simple random global event affecting the country's budget slightly
    tg_id = update.effective_user.id
    user = get_user(tg_id)
    if not user:
        await update.message.reply_text("Вы не зарегистрированы. Отправьте /start")
        return
    events = [
        ("Бурный рост цен на ресурсы", -100),
        ("Инвесторы увеличили интерес к вашей фракции", +150),
        ("Региональная нестабильность уменьшила торговлю", -80),
        ("Международная поддержка — грант", +120),
    ]
    ev, delta = random.choice(events)
    update_user_balance(tg_id, delta)
    append_history(tg_id, {"action": f"event: {ev}", "cost": -delta, "result": ev})
    await update.message.reply_text(f"Событие: {ev}\nИзменение бюджета: {delta}. Текущий баланс: {get_user(tg_id)['balance']}")


async def history_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE):
    tg_id = update.effective_user.id
    user = get_user(tg_id)
    if not user:
        await update.message.reply_text("Вы не зарегистрированы. Отправьте /start")
        return
    lines = []
    for i, e in enumerate(user["history"][-10:], 1):
        lines.append(f"{i}. {e.get('action')} -> {e.get('result')} (cost {e.get('cost')})")
    if not lines:
        await update.message.reply_text("История пуста")
    else:
        await update.message.reply_text("\n".join(lines))


async def remember_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE):
    tg_id = update.effective_user.id
    user = get_user(tg_id)
    if not user:
        await update.message.reply_text("Вы не зарегистрированы. Отправьте /start")
        return
    note = " ".join(context.args).strip()
    if not note:
        await update.message.reply_text("Использование: /remember <что запомнить>")
        return
    remember_note(tg_id, note)
    await update.message.reply_text(f"Запомнено: {note}")


async def memory_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE):
    tg_id = update.effective_user.id
    user = get_user(tg_id)
    if not user:
        await update.message.reply_text("Вы не зарегистрированы. Отправьте /start")
        return
    notes = get_memories_for_user(tg_id)
    if not notes:
        await update.message.reply_text("Память пуста. Используйте /remember, чтобы добавить заметку.")
        return
    lines = [f"{idx+1}. {note['note']}" for idx, note in enumerate(notes)]
    await update.message.reply_text("Память:\n" + "\n".join(lines))


async def help_cmd(update: Update, context: ContextTypes.DEFAULT_TYPE):
    await update.message.reply_text(
        "Команды:\n"
        "/start — информация и как зарегистрировать страну\n"
        "/register <ник> <страна> — зарегистрировать страну и ник\n"
        "/profile — показать страну, ник и бюджет\n"
        "/leave — выйти из страны\n"
        "/prices — посмотреть цены на действия\n"
        "/budget — как расходовать бюджет\n"
        "/act <действие> — потратить деньги на действие\n"
        "/history — показать последние действия\n"
        "/remember <текст> — добавить заметку в память\n"
        "/memory — посмотреть сохранённые заметки\n"
        "/events — случайное геополитическое событие\n"
        "/addbudget <telegram_id|страна|ник> <сумма> — админ добавляет средства\n"
        "/help — показать список команд\n"
        "RP в группе Telegram работает через команды бота."
    )


async def unknown(update: Update, context: ContextTypes.DEFAULT_TYPE):
    await update.message.reply_text("Неизвестная команда. Используйте /help для списка команд.")


def main():
    if not TELEGRAM_TOKEN:
        raise RuntimeError("Set TELEGRAM_TOKEN in environment or .env file")
    app = ApplicationBuilder().token(TELEGRAM_TOKEN).build()

    app.add_handler(CommandHandler("start", start))
    app.add_handler(CommandHandler("register", register_cmd))
    app.add_handler(CommandHandler("profile", profile))
    app.add_handler(CommandHandler("budget", budget_cmd))
    app.add_handler(CommandHandler("prices", prices_cmd))
    app.add_handler(CommandHandler("act", act_cmd))
    app.add_handler(CommandHandler("leave", leave_cmd))
    app.add_handler(CommandHandler("addbudget", addbudget_cmd))
    app.add_handler(CommandHandler("transfer", transfer_cmd))
    app.add_handler(CommandHandler("events", events_cmd))
    app.add_handler(CommandHandler("remember", remember_cmd))
    app.add_handler(CommandHandler("memory", memory_cmd))
    app.add_handler(CommandHandler("history", history_cmd))
    app.add_handler(CommandHandler("help", help_cmd))
    app.add_error_handler(error_handler)
    app.add_handler(MessageHandler(filters.COMMAND, unknown))

    logger.info("Starting bot")
    app.run_polling()


if __name__ == "__main__":
    main()
