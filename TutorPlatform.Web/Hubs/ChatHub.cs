using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;

namespace TutorPlatform.Web.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public ChatHub(AppDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // Client gọi: connection.invoke("SendMessage", receiverId, content)
    public async Task SendMessage(string receiverId, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        var sender = await _userManager.GetUserAsync(Context.User!);
        if (sender == null) return;

        // 1. Lưu DB
        var message = new Message
        {
            SenderId = sender.Id,
            ReceiverId = receiverId,
            Content = content.Trim(),
            SentAt = DateTime.UtcNow,
            IsRead = false
        };
        _db.Messages.Add(message);
        await _db.SaveChangesAsync();

        // 2. Payload trả về client
        var payload = new
        {
            id = message.Id,
            senderId = sender.Id,
            senderName = sender.FullName,
            content = message.Content,
            sentAt = message.SentAt.ToString("HH:mm")
        };

        // 3. Push tới cả 2 phía ngay lập tức
        await Clients.User(sender.Id).SendAsync("ReceiveMessage", payload);
        await Clients.User(receiverId).SendAsync("ReceiveMessage", payload);

        // 4. Thông báo cho người nhận (gộp: 1 thông báo chưa đọc / mỗi người gửi để tránh spam)
        var link = "/Message/Chat/" + sender.Id;
        var preview = message.Content.Length > 40 ? message.Content.Substring(0, 40) + "…" : message.Content;
        var existing = await _db.Notifications.FirstOrDefaultAsync(n =>
            n.UserId == receiverId && !n.IsRead && n.Link == link);
        if (existing != null)
        {
            existing.Content = $"{sender.FullName}: {preview}";
            existing.CreatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.Notifications.Add(new Notification
            {
                UserId = receiverId,
                Title = "Tin nhắn mới",
                Content = $"{sender.FullName}: {preview}",
                Link = link,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();
    }

    // Đánh dấu đã đọc
    public async Task MarkAsRead(string partnerId)
    {
        var currentUserId = _userManager.GetUserId(Context.User!);
        var unread = _db.Messages.Where(m =>
            m.SenderId == partnerId &&
            m.ReceiverId == currentUserId &&
            !m.IsRead);

        await foreach (var m in unread.AsAsyncEnumerable())
            m.IsRead = true;

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Gọi từ server để push thông báo tới 1 user cụ thể.
    /// VD: BadgeService, BookingController đều có thể gọi hàm này.
    /// </summary>
    public static async Task SendNotificationToUser(
        IHubContext<ChatHub> hubContext,
        string userId,
        string title,
        string content,
        string link = "/")
    {
        await hubContext.Clients.User(userId).SendAsync("ReceiveNotification", new
        {
            title,
            content,
            link,
            time = DateTime.Now.ToString("HH:mm dd/MM")
        });
    }

}