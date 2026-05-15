using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Services;

public class SelfRepairService
{
    private readonly ILogger<SelfRepairService> _logger;
    private readonly Database _db;
    private readonly string? _openAIKey;
    private readonly HttpClient _httpClient;
    private readonly string _logPath = "selfrepair.log";

    public SelfRepairService(IConfiguration config, ILogger<SelfRepairService> logger, Database db, HttpClient httpClient)
    {
        _logger = logger;
        _db = db;
        _openAIKey = config["OpenAIApiKey"];
        _httpClient = httpClient;
    }

    public async Task HandleErrorAsync(Exception ex)
    {
        _logger.LogError(ex, "Error occurred, attempting self-repair");
        
        var errorMsg = ex.Message;
        var stackTrace = ex.StackTrace ?? "";
        
        // Log to file
        await LogErrorToFile(errorMsg, stackTrace);
        
        // Log to DB
        string? proposedFix = null;
        if (!string.IsNullOrWhiteSpace(_openAIKey))
        {
            proposedFix = await GenerateFixAsync(errorMsg, stackTrace);
        }
        
        _db.LogError(errorMsg, stackTrace, proposedFix);
        
        if (!string.IsNullOrWhiteSpace(proposedFix))
        {
            _logger.LogInformation("Proposed fix: {Fix}", proposedFix);
        }
    }

    private async Task LogErrorToFile(string errorMsg, string stackTrace)
    {
        try
        {
            var line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {errorMsg}\n{stackTrace}\n---\n";
            await File.AppendAllTextAsync(_logPath, line, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log error to file");
        }
    }

    private async Task<string?> GenerateFixAsync(string errorMsg, string stackTrace)
    {
        try
        {
            var prompt = $@"You are a C# code expert. A Telegram bot encountered this error:

Error: {errorMsg}
StackTrace: {stackTrace}

Provide a concise one-line fix or suggested approach (max 150 characters, in Russian).";

            var request = new
            {
                model = "gpt-3.5-turbo",
                messages = new[] { new { role = "user", content = prompt } },
                max_tokens = 100,
                temperature = 0.5
            };

            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_openAIKey}");

            var resp = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var choices = doc.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() > 0)
            {
                return choices[0].GetProperty("message").GetProperty("content").GetString()?.Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate fix via OpenAI");
        }

        return null;
    }

    public List<(int Id, string Error, string? Fix)> GetUnfixedErrors(int limit = 5)
    {
        var errors = _db.GetUnfixedErrors(limit);
        return errors.Select(e => (e.Id, e.ErrorMessage ?? "Unknown", e.ProposedFix)).ToList();
    }

    public void MarkErrorAsFixed(int errorId)
    {
        _db.MarkErrorFixed(errorId);
        _logger.LogInformation("Marked error {ErrorId} as fixed", errorId);
    }
}
