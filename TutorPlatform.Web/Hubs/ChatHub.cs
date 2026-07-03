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
    private readonly TutorPlatform.Web.Services.ModerationService _moderation;

    // #11: theo dõi online (userId -> số kết nối)
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _online = new();
    public static bool IsUserOnline(string userId) => _online.TryGetValue(userId, out var n) && n > 0;

    public override async Task OnConnectedAsync()
    {
        var uid = Context.UserIdentifier;
        if (uid != null)
        {
            _online.AddOrUpdate(uid, 1, (_, n) => n + 1);
            await Clients.All.SendAsync("PresenceChanged", uid, true);
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var uid = Context.UserIdentifier;
        if (uid != null)
        {
            var n = _online.AddOrUpdate(uid, 0, (_, x) => Math.Max(0, x - 1));
            if (n == 0) await Clients.All.SendAsync("PresenceChanged", uid, false);
        }
        await base.OnDisconnectedAsync(exception);
    }

    // #11: báo "đang gõ..." cho người nhận
    public Task Typing(string receiverId)
        => Clients.User(receiverId).SendAsync("Typing", Context.UserIdentifier);

    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public ChatHub(AppDbContext db, UserManager<AppUser> userManager, TutorPlatform.Web.Services.ModerationService moderation)
    {
        _moderation = moderation;
        _db = db;
        _userManager = userManager;
    }

    // Client gọi: connection.invoke("SendMessage", receiverId, content)
    public async Task SendMessage(string receiverId, string content)
    {
        // #18: kiểm duyệt nhanh (regex) — chặn SĐT/chuyển khoản/giao dịch ngoài/tục tĩu
        var mod = _moderation.QuickCheck(content);
        if (!mod.Ok)
        {
            await Clients.Caller.SendAsync("MessageBlocked", mod.Reason);
            return;
        }

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