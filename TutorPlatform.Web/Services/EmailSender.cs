using System.Text;
using System.Text.Json;

namespace TutorPlatform.Web.Services;

// Gửi email qua Brevo HTTP API (port 443) — vì Render free chặn cổng SMTP.
public class EmailSender
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailSender> _logger;
    private readonly IHttpClientFactory _httpFactory;

    public EmailSender(IConfiguration config, ILogger<EmailSender> logger, IHttpClientFactory httpFactory)
    {
        _config = config;
        _logger = logger;
        _httpFactory = httpFactory;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        var apiKey = _config["Email:BrevoApiKey"];
        var senderEmail = _config["Email:SenderEmail"];
        var senderName = _config["Email:SenderName"] ?? "Gia Sư Việt";

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(senderEmail))
        {
            _logger.LogWarning("Chưa cấu hình Email:BrevoApiKey / Email:SenderEmail.");
            throw new InvalidOperationException("Chưa cấu hình email gửi đi.");
        }

        var payload = new
        {
            sender = new { name = senderName, email = senderEmail },
            to = new[] { new { email = toEmail } },
            subject = subject,
            htmlContent = htmlBody
        };

        var client = _httpFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
        request.Headers.Add("api-key", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError("Gửi email thất bại ({Status}): {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"Brevo trả lỗi {(int)response.StatusCode}: {body}");
        }

        _logger.LogInformation("Đã gửi email tới {Email}", toEmail);
    }
}
