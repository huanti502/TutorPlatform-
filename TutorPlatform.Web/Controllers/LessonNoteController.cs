using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.Services;

namespace TutorPlatform.Web.Controllers;

[Authorize]
public class LessonNoteController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly AIService _ai;
    private readonly NotificationService _notif;

    public LessonNoteController(AppDbContext db, UserManager<AppUser> userManager,
        AIService ai, NotificationService notif)
    {
        _db = db;
        _userManager = userManager;
        _ai = ai;
        _notif = notif;
    }

    private async Task<Booking?> LoadBookingAsync(int bookingId) =>
        await _db.Bookings
            .Include(b => b.Subject)
            .Include(b => b.Student)
            .Include(b => b.TutorProfile).ThenInclude(t => t.User)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

    // ===================== GIA SƯ =====================

    [Authorize(Roles = "Tutor")]
    [HttpGet]
    public async Task<IActionResult> Edit(int bookingId)
    {
        var uid = _userManager.GetUserId(User)!;
        var booking = await LoadBookingAsync(bookingId);
        if (booking == null || booking.TutorProfile.UserId != uid)
            return RedirectToAction("TutorRequests", "Booking");

        var note = await _db.LessonNotes.FirstOrDefaultAsync(n => n.BookingId == bookingId);
        ViewBag.Booking = booking;
        return View(note ?? new LessonNote { BookingId = bookingId });
    }

    [Authorize(Roles = "Tutor")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int bookingId, string? content)
    {
        var uid = _userManager.GetUserId(User)!;
        var booking = await LoadBookingAsync(bookingId);
        if (booking == null || booking.TutorProfile.UserId != uid)
            return RedirectToAction("TutorRequests", "Booking");

        var note = await GetOrCreateNoteAsync(booking, uid);
        note.Content = content;
        note.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _notif.NotifyAsync(booking.StudentId, "📝 Ghi chú buổi học",
            $"Gia sư đã cập nhật ghi chú buổi {booking.Subject?.Name}.", $"/LessonNote/View?bookingId={bookingId}");

        TempData["Success"] = "Đã lưu ghi chú.";
        return RedirectToAction("Edit", new { bookingId });
    }

    [Authorize(Roles = "Tutor")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Summarize(int bookingId)
    {
        var uid = _userManager.GetUserId(User)!;
        var booking = await LoadBookingAsync(bookingId);
        if (booking == null || booking.TutorProfile.UserId != uid)
            return RedirectToAction("TutorRequests", "Booking");

        var note = await _db.LessonNotes.FirstOrDefaultAsync(n => n.BookingId == bookingId);
        if (note == null || string.IsNullOrWhiteSpace(note.Content))
        {
            TempData["Error"] = "Hãy nhập và lưu ghi chú trước khi tóm tắt.";
            return RedirectToAction("Edit", new { bookingId });
        }

        try
        {
            var system = "Bạn là trợ lý giáo dục. Hãy tóm tắt ghi chú buổi học bằng tiếng Việt thành đúng 3 mục, " +
                         "mỗi mục vài gạch đầu dòng ngắn gọn: '📘 Đã học', '🔁 Cần ôn tập', '💡 Gợi ý luyện tập'. " +
                         "Không thêm lời mở đầu hay kết luận.";
            var summary = await _ai.ChatAsync(system,
                $"Môn: {booking.Subject?.Name}\nGhi chú của gia sư:\n{note.Content}", 800);

            note.AiSummary = summary;
            note.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Đã tạo bản tóm tắt bằng AI.";
        }
        catch
        {
            TempData["Error"] = "Không gọi được AI lúc này, thử lại sau.";
        }

        return RedirectToAction("Edit", new { bookingId });
    }

    // ===================== HỌC VIÊN =====================

    [Authorize(Roles = "Student")]
    [HttpGet]
    public async Task<IActionResult> View(int bookingId)
    {
        var uid = _userManager.GetUserId(User)!;
        var booking = await LoadBookingAsync(bookingId);
        if (booking == null || booking.StudentId != uid)
            return RedirectToAction("MyBookings", "Booking");

        var note = await _db.LessonNotes.FirstOrDefaultAsync(n => n.BookingId == bookingId);
        ViewBag.Booking = booking;
        return View(note);
    }

    private async Task<LessonNote> GetOrCreateNoteAsync(Booking booking, string tutorId)
    {
        var note = await _db.LessonNotes.FirstOrDefaultAsync(n => n.BookingId == booking.Id);
        if (note == null)
        {
            note = new LessonNote
            {
                BookingId = booking.Id,
                TutorId = tutorId,
                StudentId = booking.StudentId,
                CreatedAt = DateTime.UtcNow
            };
            _db.LessonNotes.Add(note);
        }
        return note;
    }
}
