using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.Hubs;

namespace TutorPlatform.Web.Services;

/// <summary>
/// Gom việc tạo thông báo về 1 chỗ duy nhất:
///  - Lưu 1 dòng vào bảng Notifications (để xem lại sau)
///  - Push realtime qua SignalR (ChatHub) để chuông nhảy ngay
/// Mọi nơi (Booking, Review, Payment, Message, Admin...) chỉ cần gọi NotifyAsync(...).
/// </summary>
public class NotificationService
{
    private readonly AppDbContext _db;
    private readonly IHubContext<ChatHub> _hub;

    public NotificationService(AppDbContext db, IHubContext<ChatHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    /// <summary>
    /// Tạo + đẩy 1 thông báo cho 1 user.
    /// </summary>
    public async Task NotifyAsync(string userId, string title, string content, string? link = "/")
    {
        if (string.IsNullOrWhiteSpace(userId)) return;

        var notif = new Notification
        {
            UserId = userId,
            Title = title,
            Content = content,
            Link = link,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Notifications.Add(notif);
        await _db.SaveChangesAsync();

        // Push realtime — JS trong _Layout đang nghe sự kiện "ReceiveNotification"
        await _hub.Clients.User(userId).SendAsync("ReceiveNotification", new
        {
            id = notif.Id,
            title,
            content,
            link,
            time = DateTime.Now.ToString("HH:mm dd/MM")
        });
    }

    /// <summary>Gửi cùng 1 thông báo cho nhiều user (vd: tất cả Admin).</summary>
    public async Task NotifyManyAsync(IEnumerable<string> userIds, string title, string content, string? link = "/")
    {
        foreach (var id in userIds.Distinct())
            await NotifyAsync(id, title, content, link);
    }

    /// <summary>Số thông báo chưa đọc của 1 user.</summary>
    public Task<int> UnreadCountAsync(string userId) =>
        _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
}
