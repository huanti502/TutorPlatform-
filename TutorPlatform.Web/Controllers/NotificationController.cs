using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;

namespace TutorPlatform.Web.Controllers;

/// <summary>
/// API JSON phục vụ chuông thông báo (dropdown) trên _Layout.
/// Trang xem-tất-cả vẫn nằm ở Account/Notifications.
/// </summary>
[Authorize]
public class NotificationController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public NotificationController(AppDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // GET /Notification/UnreadCount  -> { count: 3 }
    [HttpGet]
    public async Task<IActionResult> UnreadCount()
    {
        var uid = _userManager.GetUserId(User)!;
        var count = await _db.Notifications.CountAsync(n => n.UserId == uid && !n.IsRead);
        return Json(new { count });
    }

    // GET /Notification/Recent  -> 8 thông báo mới nhất cho dropdown
    [HttpGet]
    public async Task<IActionResult> Recent()
    {
        var uid = _userManager.GetUserId(User)!;
        var items = await _db.Notifications
            .Where(n => n.UserId == uid)
            .OrderByDescending(n => n.CreatedAt)
            .Take(8)
            .Select(n => new
            {
                n.Id,
                n.Title,
                n.Content,
                n.Link,
                n.IsRead,
                n.CreatedAt
            })
            .ToListAsync();

        var unread = items.Count(i => !i.IsRead);

        var result = items.Select(n => new
        {
            n.Id,
            n.Title,
            n.Content,
            link = string.IsNullOrEmpty(n.Link) ? "/" : n.Link,
            n.IsRead,
            timeAgo = TimeAgo(n.CreatedAt)
        });

        return Json(new { unread, items = result });
    }

    // POST /Notification/MarkRead  (id)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id)
    {
        var uid = _userManager.GetUserId(User)!;
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid);
        if (n != null && !n.IsRead)
        {
            n.IsRead = true;
            await _db.SaveChangesAsync();
        }
        var count = await _db.Notifications.CountAsync(x => x.UserId == uid && !x.IsRead);
        return Json(new { ok = true, count });
    }

    // POST /Notification/MarkAllRead
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var uid = _userManager.GetUserId(User)!;
        var unread = await _db.Notifications.Where(n => n.UserId == uid && !n.IsRead).ToListAsync();
        foreach (var n in unread) n.IsRead = true;
        await _db.SaveChangesAsync();
        return Json(new { ok = true, count = 0 });
    }

    private static string TimeAgo(DateTime utc)
    {
        var diff = DateTime.UtcNow - utc;
        if (diff.TotalMinutes < 1) return "Vừa xong";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} phút trước";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} giờ trước";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} ngày trước";
        // +7 múi giờ VN cho ngày hiển thị
        return utc.AddHours(7).ToString("dd/MM/yyyy");
    }
}
