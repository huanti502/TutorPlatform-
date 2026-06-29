// AIService.cs — Dùng Groq API (MIỄN PHÍ)
// Model: llama-3.3-70b-versatile

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TutorPlatform.Web.Services;

public class AIService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger<AIService> _logger;
    private const string MODEL = "openai/gpt-oss-120b";   // Cập nhật: llama-3.3-70b-versatile đã bị Groq ngừng hỗ trợ (17/6/2026)

    public AIService(IConfiguration config, ILogger<AIService> logger)
    {
        _apiKey = config["Groq:ApiKey"]
            ?? throw new Exception("Thiếu Groq:ApiKey trong appsettings.json");
        _logger = logger;

        _http = new HttpClient
        {
            BaseAddress = new Uri("https://api.groq.com")
        };
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public async Task<string> ChatAsync(
        string systemPrompt,
        string userMessage,
        int maxTokens = 1500,
        string model = MODEL)
    {
        var body = new
        {
            model = MODEL,
            max_tokens = maxTokens,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user",   content = userMessage  }
            }
        };
        return await CallGroqAsync(body);
    }

    public async Task<string> MultiTurnChatAsync(
        string systemPrompt,
        List<(string role, string content)> messages,
        int maxTokens = 1500)
    {
        var msgList = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };
        foreach (var (role, content) in messages)
            msgList.Add(new { role, content });

        var body = new
        {
            model = MODEL,
            max_tokens = maxTokens,
            messages = msgList
        };
        return await CallGroqAsync(body);
    }

    private async Task<string> CallGroqAsync(object body)
    {
        var json = JsonSerializer.Serialize(body);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _http.PostAsync("/openai/v1/chat/completions", httpContent);
            var raw = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Groq API lỗi: {status} — {body}", response.StatusCode, raw);
                throw new Exception($"Groq API lỗi {response.StatusCode}");
            }

            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi Groq API");
            throw;
        }
    }
}