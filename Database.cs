using Microsoft.Data.Sqlite;
using Dapper;
using System.Text.Json;

namespace Services;

public class UserRecord
{
    public int Id { get; set; }
    public long TelegramId { get; set; }
    public string? Nickname { get; set; }
    public string? Country { get; set; }
    public int Balance { get; set; }
}

public class HistoryEntry
{
    public int Id { get; set; }
    public long TelegramId { get; set; }
    public string? Action { get; set; }
    public int Cost { get; set; }
    public string? Result { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MemoryEntry
{
    public int Id { get; set; }
    public long TelegramId { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ErrorLog
{
    public int Id { get; set; }
    public string? ErrorMessage { get; set; }
    public string? StackTrace { get; set; }
    public string? ProposedFix { get; set; }
    public bool Applied { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Database
{
    private readonly string _path;
    private readonly string _connStr;

    public Database(Microsoft.Extensions.Configuration.IConfiguration config)
    {
        _path = config["DatabasePath"] ?? "georp_csharp.db";
        _connStr = $"Data Source={_path}";
        EnsureTables();
    }

    private void EnsureTables()
    {
        using var c = new SqliteConnection(_connStr);
        c.Execute(@"CREATE TABLE IF NOT EXISTS users (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            telegram_id INTEGER UNIQUE,
            nickname TEXT,
            country TEXT,
            balance INTEGER DEFAULT 0,
            created_at DATETIME DEFAULT CURRENT_TIMESTAMP
        )");
        c.Execute(@"CREATE TABLE IF NOT EXISTS history (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            telegram_id INTEGER,
            action TEXT,
            cost INTEGER,
            result TEXT,
            created_at DATETIME DEFAULT CURRENT_TIMESTAMP
        )");
        c.Execute(@"CREATE TABLE IF NOT EXISTS memories (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            telegram_id INTEGER,
            note TEXT,
            created_at DATETIME DEFAULT CURRENT_TIMESTAMP
        )");
        c.Execute(@"CREATE TABLE IF NOT EXISTS error_logs (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            error_message TEXT,
            stack_trace TEXT,
            proposed_fix TEXT,
            applied INTEGER DEFAULT 0,
            created_at DATETIME DEFAULT CURRENT_TIMESTAMP
        )");
    }

    public UserRecord? GetUser(long telegramId)
    {
        using var c = new SqliteConnection(_connStr);
        return c.QuerySingleOrDefault<UserRecord>("SELECT id, telegram_id, nickname, country, balance FROM users WHERE telegram_id = @tg", new { tg = telegramId });
    }

    public UserRecord? GetUserByCountry(string country)
    {
        using var c = new SqliteConnection(_connStr);
        return c.QuerySingleOrDefault<UserRecord>(
            "SELECT id, telegram_id, nickname, country, balance FROM users WHERE LOWER(country) = LOWER(@cnt) OR LOWER(nickname) = LOWER(@cnt) LIMIT 1",
            new { cnt = country });
    }

    public void CreateOrUpdateUser(long telegramId, string nickname, string country)
    {
        using var c = new SqliteConnection(_connStr);
        var existing = GetUser(telegramId);
        if (existing is null)
        {
            c.Execute("INSERT INTO users (telegram_id, nickname, country, balance) VALUES (@tg, @nick, @cnt, 0)", 
                new { tg = telegramId, nick = nickname, cnt = country });
        }
        else
        {
            c.Execute("UPDATE users SET nickname = @nick, country = @cnt, balance = 0 WHERE telegram_id = @tg", 
                new { tg = telegramId, nick = nickname, cnt = country });
        }
    }

    public void LeaveCountry(long telegramId)
    {
        using var c = new SqliteConnection(_connStr);
        c.Execute("UPDATE users SET country = NULL, balance = 0 WHERE telegram_id = @tg", new { tg = telegramId });
    }

    public void UpdateBalance(long telegramId, int delta)
    {
        using var c = new SqliteConnection(_connStr);
        c.Execute("UPDATE users SET balance = balance + @delta WHERE telegram_id = @tg", new { delta, tg = telegramId });
    }

    public void AddHistory(long telegramId, string action, int cost, string result)
    {
        using var c = new SqliteConnection(_connStr);
        c.Execute("INSERT INTO history (telegram_id, action, cost, result) VALUES (@tg, @act, @cost, @result)", 
            new { tg = telegramId, act = action, cost, result });
    }

    public List<HistoryEntry> GetHistory(long telegramId, int limit = 5)
    {
        using var c = new SqliteConnection(_connStr);
        return c.Query<HistoryEntry>(
            "SELECT id, telegram_id, action, cost, result, created_at FROM history WHERE telegram_id = @tg ORDER BY created_at DESC LIMIT @limit",
            new { tg = telegramId, limit }).ToList();
    }

    public void RememberNote(long telegramId, string note)
    {
        using var c = new SqliteConnection(_connStr);
        c.Execute("INSERT INTO memories (telegram_id, note) VALUES (@tg, @note)", new { tg = telegramId, note });
    }

    public List<MemoryEntry> GetMemories(long telegramId, int limit = 5)
    {
        using var c = new SqliteConnection(_connStr);
        return c.Query<MemoryEntry>(
            "SELECT id, telegram_id, note, created_at FROM memories WHERE telegram_id = @tg ORDER BY created_at DESC LIMIT @limit",
            new { tg = telegramId, limit }).ToList();
    }

    public string GetMemorySummary(long telegramId)
    {
        var notes = GetMemories(telegramId, 5);
        if (!notes.Any()) return "";
        return string.Join("\n", notes.Select(n => $"- {n.Note}"));
    }

    public void LogError(string errorMsg, string stackTrace, string? proposedFix = null)
    {
        using var c = new SqliteConnection(_connStr);
        c.Execute(
            "INSERT INTO error_logs (error_message, stack_trace, proposed_fix) VALUES (@msg, @st, @fix)",
            new { msg = errorMsg, st = stackTrace, fix = proposedFix });
    }

    public List<ErrorLog> GetUnfixedErrors(int limit = 10)
    {
        using var c = new SqliteConnection(_connStr);
        return c.Query<ErrorLog>(
            "SELECT id, error_message, stack_trace, proposed_fix, applied, created_at FROM error_logs WHERE applied = 0 ORDER BY created_at DESC LIMIT @limit",
            new { limit }).ToList();
    }

    public void MarkErrorFixed(int errorId)
    {
        using var c = new SqliteConnection(_connStr);
        c.Execute("UPDATE error_logs SET applied = 1 WHERE id = @id", new { id = errorId });
    }
}
