using System.Net;
using System.Net.Mail;

namespace TutorPlatform.Web.Services;

// Gửi email qua SMTP Gmail (dùng App Password 16 ký tự).
public class EmailSender
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(IConfiguration config, ILogger<EmailSender> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        var senderEmail = _config["Email:SenderEmail"];
        var appPassword = _config["Email:AppPassword"];
        var senderName = _config["Email:SenderName"] ?? "Gia Sư Việt";

        if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(appPassword))
        {
            _logger.LogWarning("Chưa cấu hình Email:SenderEmail / Email:AppPassword — bỏ qua gửi mail.");
            throw new InvalidOperationException("Chưa cấu hình email gửi đi.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(senderEmail, senderName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient("smtp.gmail.com", 587)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(senderEmail, appPassword)
        };

        await client.SendMailAsync(message);
        _logger.LogInformation("Đã gửi email tới {Email}", toEmail);
    }
}
