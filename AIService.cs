using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text.Json;

namespace Services;

public class AIService
{
    private readonly ILogger<AIService> _logger;
    private readonly string? _openAIKey;
    private readonly HttpClient _httpClient;

    private readonly List<string> _fallbackTemplates = new()
    {
        "{actor} предпринимает шаг: {action}. Это вызвало ответ от соседей — {effect}.",
        "Дипломатические каналы реагируют на {action} {actor} — результат: {effect}.",
        "{actor} использует ресурсы для {action}. В краткосрочной перспективе: {effect}.",
        "Геополитическая карта меняется: {actor} делает {action}. Прогноз: {effect}.",
    };

    private readonly List<string> _effects = new()
    {
        "экономическое давление",
        "рост влияния",
        "обострение конфликтов",
        "усиление сотрудничества",
        "волатильность рынка",
        "дипломатический кризис",
        "торговый альянс",
        "военные учения соседей",
    };

    public AIService(IConfiguration config, ILogger<AIService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _openAIKey = config["OpenAIApiKey"];
        _httpClient = httpClient;
    }

    public async Task<string> GenerateResponse(string action, string country, string? memory = null)
    {
        if (!string.IsNullOrWhiteSpace(_openAIKey))
        {
            try
            {
                var response = await GenerateViaOpenAI(action, country, memory);
                if (!string.IsNullOrWhiteSpace(response))
                    return response;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OpenAI call failed, using fallback");
            }
        }

        return GenerateFallback(action, country);
    }

    private async Task<string> GenerateViaOpenAI(string action, string country, string? memory)
    {
        var systemPrompt = "Ты движок геополитического RP. Отвечай по-русски в стиле RP, кратко (2-3 предложения), указывай последствия действия.";
        var userPrompt = $"Страна: {country}\nДействие: {action}";
        if (!string.IsNullOrWhiteSpace(memory))
            userPrompt += $"\nПредыдущие события:\n{memory}";

        var request = new
        {
            model = "gpt-3.5-turbo",
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            max_tokens = 150,
            temperature = 0.7
        };

        var content = new StringContent(JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json");
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_openAIKey}");

        var resp = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("OpenAI API returned {StatusCode}", resp.StatusCode);
            return "";
        }

        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var choices = doc.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() > 0)
        {
            var text = choices[0].GetProperty("message").GetProperty("content").GetString();
            return text?.Trim() ?? "";
        }

        return "";
    }

    private string GenerateFallback(string action, string country)
    {
        var template = _fallbackTemplates[new Random().Next(_fallbackTemplates.Count)];
        var effect = _effects[new Random().Next(_effects.Count)];

        if (action.ToLower().Contains("санкц"))
            effect = new Random().Next(2) == 0 ? "экономическое давление" : "торговые ограничения";

        return template
            .Replace("{actor}", country ?? "Ваша страна")
            .Replace("{action}", action)
            .Replace("{effect}", effect);
    }
}
