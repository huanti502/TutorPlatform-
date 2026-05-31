using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;

namespace TutorPlatform.Web.Controllers;

[Authorize] // Bắt buộc đăng nhập
public class MessageController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public MessageController(AppDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // Hiển thị danh sách các cuộc trò chuyện
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);

        // Lấy tất cả tin nhắn liên quan đến user hiện tại
        var allMessages = await _db.Messages
            .Where(m => m.SenderId == user!.Id || m.ReceiverId == user!.Id)
            .OrderByDescending(m => m.SentAt)
            .ToListAsync();

        // Lấy danh sách ID của những người đã trò chuyện
        var partnerIds = allMessages
            .Select(m => m.SenderId == user!.Id ? m.ReceiverId : m.SenderId)
            .Distinct()
            .ToList();

        // Lấy thông tin của các partner đó
        var partners = await _db.Users
            .Where(u => partnerIds.Contains(u.Id))
            .ToListAsync();

        // Đếm số tin nhắn chưa đọc
        ViewBag.UnreadCount = await _db.Messages
            .CountAsync(m => m.ReceiverId == user!.Id && !m.IsRead);

        return View(partners);
    }

    // Hiển thị khung chat với 1 người cụ thể
    public async Task<IActionResult> Chat(string id)
    {
        var currentUserId = _userManager.GetUserId(User);
        var partner = await _userManager.FindByIdAsync(id);

        if (partner == null) return NotFound();

        // Lấy lịch sử tin nhắn giữa 2 người
        var messages = await _db.Messages
            .Where(m => (m.SenderId == currentUserId && m.ReceiverId == id) ||
                        (m.SenderId == id && m.ReceiverId == currentUserId))
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        // Đánh dấu các tin nhắn người kia gửi cho mình là "Đã đọc"
        var unreadMessages = messages.Where(m => m.ReceiverId == currentUserId && !m.IsRead).ToList();
        if (unreadMessages.Any())
        {
            foreach (var msg in unreadMessages)
            {
                msg.IsRead = true;
            }
            await _db.SaveChangesAsync();
        }

        ViewBag.Partner = partner;
        ViewBag.CurrentUserId = currentUserId;

        return View(messages);
    }

    // Xử lý gửi tin nhắn
    [HttpPost]
    public async Task<IActionResult> Send(string receiverId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return RedirectToAction("Chat", new { id = receiverId });

        var senderId = _userManager.GetUserId(User);

        var message = new Message
        {
            SenderId = senderId!,
            ReceiverId = receiverId,
            Content = content,
            SentAt = DateTime.UtcNow,
            IsRead = false
        };

        _db.Messages.Add(message);
        await _db.SaveChangesAsync();

        return RedirectToAction("Chat", new { id = receiverId });
    }
}