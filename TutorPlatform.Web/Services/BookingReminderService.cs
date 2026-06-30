using Microsoft.EntityFrameworkCore;
using TutorPlatform.Infrastructure.Data;

namespace TutorPlatform.Web.Services;

/// <summary>
/// Dịch vụ chạy nền: cứ mỗi vài phút quét các buổi học đã "Confirmed"
/// sắp bắt đầu trong khoảng nhắc trước (mặc định 60 phút) mà chưa được nhắc,
/// rồi gửi email (Brevo) + thông báo in-app cho cả học viên và gia sư.
///
/// Lưu ý hạ tầng: Render Free sẽ "ngủ" khi không có request -> vòng lặp chỉ
/// chạy lúc app còn thức. Muốn nhắc ổn định, đặt một cron bên ngoài
/// (vd cron-job.org) ping site mỗi ~10 phút để giữ app thức.
/// </summary>
public class BookingReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingReminderService> _logger;

    // Quét mỗi 5 phút; nhắc các buổi bắt đầu trong vòng 60 phút tới.
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ReminderWindow = TimeSpan.FromMinutes(60);

    public BookingReminderService(IServiceScopeFactory scopeFactory, ILogger<BookingReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Chờ một chút cho app khởi động xong (migrate/seed) rồi mới chạy.
        try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BookingReminderService lỗi khi quét nhắc lịch.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<EmailSender>();
        var notif = scope.ServiceProvider.GetRequiredService<NotificationService>();

        var nowUtc = DateTime.UtcNow;
        var untilUtc = nowUtc.Add(ReminderWindow);

        var dueBookings = await db.Bookings
            .Include(b => b.Subject)
            .Include(b => b.Student)
            .Include(b => b.TutorProfile).ThenInclude(t => t.User)
            .Where(b => b.Status == "Confirmed"
                        && !b.ReminderSent
                        && b.StartTime > nowUtc
                        && b.StartTime <= untilUtc)
            .ToListAsync(ct);

        if (dueBookings.Count == 0) return;

        _logger.LogInformation("Nhắc lịch: tìm thấy {Count} buổi sắp tới.", dueBookings.Count);

        foreach (var b in dueBookings)
        {
            // Giờ VN để hiển thị (DB lưu UTC)
            var startLocal = b.StartTime.AddHours(7);
            var startStr = startLocal.ToString("HH:mm dd/MM/yyyy");
            var subjectName = b.Subject?.Name ?? "buổi học";
            var joinUrl = string.IsNullOrEmpty(b.MeetingRoomId)
                ? "https://tutorplatform-hl15.onrender.com/Booking/MyBookings"
                : $"https://meet.jit.si/{b.MeetingRoomId}";

            var student = b.Student;
            var tutorUser = b.TutorProfile?.User;

            // ── Học viên ──
            if (student != null)
            {
                var partner = tutorUser?.FullName ?? "gia sư";
                await SafeSendAsync(email, student.Email,
                    $"⏰ Nhắc lịch: {subjectName} lúc {startStr}",
                    BuildEmail(student.FullName, subjectName, partner, startStr, b.TeachingMode, joinUrl));

                await SafeNotifyAsync(notif, student.Id,
                    "⏰ Sắp đến giờ học",
                    $"{subjectName} với {partner} lúc {startStr}.",
                    "/Booking/MyBookings");
            }

            // ── Gia sư ──
            if (tutorUser != null)
            {
                var partner = student?.FullName ?? "học viên";
                await SafeSendAsync(email, tutorUser.Email,
                    $"⏰ Nhắc lịch dạy: {subjectName} lúc {startStr}",
                    BuildEmail(tutorUser.FullName, subjectName, partner, startStr, b.TeachingMode, joinUrl));

                await SafeNotifyAsync(notif, tutorUser.Id,
                    "⏰ Sắp đến giờ dạy",
                    $"{subjectName} với {partner} lúc {startStr}.",
                    "/Booking/TutorRequests");
            }

            // Đánh dấu đã nhắc để không gửi lại
            b.ReminderSent = true;
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task SafeSendAsync(EmailSender email, string? to, string subject, string html)
    {
        if (string.IsNullOrWhiteSpace(to)) return;
        try { await email.SendAsync(to, subject, html); }
        catch (Exception ex) { _logger.LogWarning(ex, "Gửi email nhắc lịch thất bại tới {To}", to); }
    }

    private async Task SafeNotifyAsync(NotificationService notif, string userId, string title, string content, string link)
    {
        try { await notif.NotifyAsync(userId, title, content, link); }
        catch (Exception ex) { _logger.LogWarning(ex, "Tạo thông báo nhắc lịch thất bại cho {User}", userId); }
    }

    private static string BuildEmail(string? name, string subject, string partner, string time, string mode, string joinUrl)
    {
        var modeText = mode == "Online" ? "Học online" : "Học trực tiếp";
        return $$"""
        <div style="font-family:Arial,sans-serif;max-width:520px;margin:auto;border:1px solid #eee;border-radius:12px;overflow:hidden">
          <div style="background:#7c3aed;color:#fff;padding:18px 22px;font-size:18px;font-weight:bold">Gia Sư Việt — Nhắc lịch học</div>
          <div style="padding:22px">
            <p>Xin chào <b>{{name}}</b>,</p>
            <p>Bạn có một buổi học sắp bắt đầu:</p>
            <table style="width:100%;font-size:14px;border-collapse:collapse">
              <tr><td style="padding:6px 0;color:#666">Môn học</td><td style="padding:6px 0"><b>{{subject}}</b></td></tr>
              <tr><td style="padding:6px 0;color:#666">Cùng với</td><td style="padding:6px 0">{{partner}}</td></tr>
              <tr><td style="padding:6px 0;color:#666">Thời gian</td><td style="padding:6px 0"><b>{{time}}</b> (giờ VN)</td></tr>
              <tr><td style="padding:6px 0;color:#666">Hình thức</td><td style="padding:6px 0">{{modeText}}</td></tr>
            </table>
            <div style="text-align:center;margin:24px 0">
              <a href="{{joinUrl}}" style="background:#7c3aed;color:#fff;text-decoration:none;padding:12px 26px;border-radius:8px;font-weight:bold">Vào phòng học</a>
            </div>
            <p style="color:#999;font-size:12px">Vui lòng có mặt đúng giờ. Email tự động, không cần trả lời.</p>
          </div>
        </div>
        """;
    }
}
