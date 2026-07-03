using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.Services;

namespace TutorPlatform.Web.Controllers;

// Trang liên hệ / gửi yêu cầu hỗ trợ tới admin.
// Cho phép cả khách chưa đăng nhập (để user gặp lỗi đăng nhập vẫn liên hệ được).
public class ContactController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly NotificationService _notif;

    public ContactController(AppDbContext db, UserManager<AppUser> userManager, NotificationService notif)
    {
        _db = db;
        _userManager = userManager;
        _notif = notif;
    }

    // GET /Contact — form liên hệ + thông tin liên hệ + FAQ
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // Điền sẵn thông tin nếu đã đăng nhập.
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            ViewBag.FullName = user?.FullName;
            ViewBag.Email = user?.Email;
            ViewBag.Phone = user?.PhoneNumber;
        }
        return View();
    }

    // POST /Contact/Send
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(string fullName, string email, string? phone,
        string category, string subject, string content)
    {
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(content))
        {
            TempData["Error"] = "Vui lòng điền đầy đủ họ tên, email, tiêu đề và nội dung.";
            return RedirectToAction("Index");
        }

        var userId = User.Identity?.IsAuthenticated == true ? _userManager.GetUserId(User) : null;

        // Chống spam: mỗi email tối đa 3 yêu cầu đang mở trong 24h.
        var since = DateTime.UtcNow.AddHours(-24);
        var recent = await _db.SupportTickets.CountAsync(t =>
            t.Email == email && t.CreatedAt >= since && t.Status == "Open");
        if (recent >= 3)
        {
            TempData["Error"] = "Bạn đã gửi nhiều yêu cầu gần đây. Vui lòng chờ phản hồi trước khi gửi thêm.";
            return RedirectToAction("Index");
        }

        var ticket = new SupportTicket
        {
            UserId = userId,
            FullName = fullName.Trim(),
            Email = email.Trim(),
            Phone = phone?.Trim(),
            Category = category,
            Subject = subject.Trim(),
            Content = content.Trim(),
            Status = "Open",
            CreatedAt = DateTime.UtcNow
        };
        _db.SupportTickets.Add(ticket);
        await _db.SaveChangesAsync();

        // Thông báo real-time cho tất cả admin.
        var admins = await _userManager.GetUsersInRoleAsync("Admin");
        await _notif.NotifyManyAsync(
            admins.Select(a => a.Id),
            "Yêu cầu hỗ trợ mới",
            $"{ticket.FullName} ({ticket.Category}): {ticket.Subject}",
            "/Admin/SupportTickets");

        TempData["Success"] = "Đã gửi yêu cầu hỗ trợ! Chúng tôi sẽ phản hồi qua email hoặc thông báo trong hệ thống.";
        return RedirectToAction(userId != null ? "MyTickets" : "Index");
    }

    // GET /Contact/MyTickets — lịch sử yêu cầu của người dùng đã đăng nhập
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> MyTickets()
    {
        var userId = _userManager.GetUserId(User)!;
        var tickets = await _db.SupportTickets
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
        return View(tickets);
    }
}
