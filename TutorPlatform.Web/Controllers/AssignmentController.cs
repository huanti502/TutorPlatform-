using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.Services;

namespace TutorPlatform.Web.Controllers;

[Authorize]
public class AssignmentController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly CloudinaryService _cloudinary;
    private readonly NotificationService _notif;

    public AssignmentController(AppDbContext db, UserManager<AppUser> userManager,
        CloudinaryService cloudinary, NotificationService notif)
    {
        _db = db;
        _userManager = userManager;
        _cloudinary = cloudinary;
        _notif = notif;
    }

    // ===================== GIA SƯ =====================

    [Authorize(Roles = "Tutor")]
    [HttpGet]
    public async Task<IActionResult> Manage()
    {
        var uid = _userManager.GetUserId(User)!;

        var assignments = await _db.Assignments
            .Include(a => a.Booking).ThenInclude(b => b!.Subject)
            .Include(a => a.Booking).ThenInclude(b => b!.Student)
            .Where(a => a.TutorId == uid)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        // Danh sách buổi học để chọn khi giao bài (buổi của gia sư này).
        var bookings = await _db.Bookings
            .Include(b => b.Subject)
            .Include(b => b.Student)
            .Include(b => b.TutorProfile)
            .Where(b => b.TutorProfile.UserId == uid
                        && (b.Status == "Confirmed" || b.Status == "Completed"))
            .OrderByDescending(b => b.StartTime)
            .ToListAsync();

        ViewBag.Bookings = bookings.Select(b => new SelectListItem
        {
            Value = b.Id.ToString(),
            Text = $"{b.Student.FullName} · {b.Subject?.Name} · {b.StartTime.AddHours(7):dd/MM HH:mm}"
        }).ToList();

        return View(assignments);
    }

    [Authorize(Roles = "Tutor")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int bookingId, string title, string? description, DateTime? dueDate)
    {
        var uid = _userManager.GetUserId(User)!;

        var booking = await _db.Bookings
            .Include(b => b.TutorProfile)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null || booking.TutorProfile.UserId != uid)
        {
            TempData["Error"] = "Không tìm thấy buổi học hợp lệ.";
            return RedirectToAction("Manage");
        }
        if (string.IsNullOrWhiteSpace(title))
        {
            TempData["Error"] = "Vui lòng nhập tiêu đề bài tập.";
            return RedirectToAction("Manage");
        }

        var assignment = new Assignment
        {
            BookingId = booking.Id,
            TutorId = uid,
            StudentId = booking.StudentId,
            Title = title.Trim(),
            Description = description,
            DueDate = dueDate.HasValue ? DateTime.SpecifyKind(dueDate.Value.AddHours(-7), DateTimeKind.Utc) : null,
            Status = "Assigned",
            CreatedAt = DateTime.UtcNow
        };
        _db.Assignments.Add(assignment);
        await _db.SaveChangesAsync();

        await _notif.NotifyAsync(booking.StudentId, "📚 Bài tập mới",
            $"Bạn được giao bài: {assignment.Title}.", "/Assignment/MyAssignments");

        TempData["Success"] = "Đã giao bài tập.";
        return RedirectToAction("Manage");
    }

    [Authorize(Roles = "Tutor")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Grade(int id, string grade, string? feedback)
    {
        var uid = _userManager.GetUserId(User)!;
        var a = await _db.Assignments.FirstOrDefaultAsync(x => x.Id == id && x.TutorId == uid);
        if (a == null) return RedirectToAction("Manage");

        a.Grade = grade;
        a.Feedback = feedback;
        a.GradedAt = DateTime.UtcNow;
        a.Status = "Graded";
        await _db.SaveChangesAsync();

        await _notif.NotifyAsync(a.StudentId, "✅ Bài tập đã được chấm",
            $"\"{a.Title}\" — Điểm: {grade}.", "/Assignment/MyAssignments");

        TempData["Success"] = "Đã chấm bài.";
        return RedirectToAction("Manage");
    }

    // ===================== HỌC VIÊN =====================

    [Authorize(Roles = "Student")]
    [HttpGet]
    public async Task<IActionResult> MyAssignments()
    {
        var uid = _userManager.GetUserId(User)!;
        var assignments = await _db.Assignments
            .Include(a => a.Booking).ThenInclude(b => b!.Subject)
            .Include(a => a.Booking).ThenInclude(b => b!.TutorProfile).ThenInclude(t => t.User)
            .Where(a => a.StudentId == uid)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
        return View(assignments);
    }

    [Authorize(Roles = "Student")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int id, string? submissionText, IFormFile? file)
    {
        var uid = _userManager.GetUserId(User)!;
        var a = await _db.Assignments.FirstOrDefaultAsync(x => x.Id == id && x.StudentId == uid);
        if (a == null) return RedirectToAction("MyAssignments");

        if (string.IsNullOrWhiteSpace(submissionText) && (file == null || file.Length == 0))
        {
            TempData["Error"] = "Vui lòng nhập nội dung hoặc đính kèm file.";
            return RedirectToAction("MyAssignments");
        }

        if (file != null && file.Length > 0)
        {
            var url = await _cloudinary.UploadFileAsync(file, "assignments");
            if (url != null) a.SubmissionFileUrl = url;
        }
        a.SubmissionText = submissionText;
        a.SubmittedAt = DateTime.UtcNow;
        a.Status = "Submitted";
        await _db.SaveChangesAsync();

        var student = await _userManager.GetUserAsync(User);
        await _notif.NotifyAsync(a.TutorId, "📝 Học viên đã nộp bài",
            $"{student?.FullName} đã nộp: {a.Title}.", "/Assignment/Manage");

        TempData["Success"] = "Đã nộp bài.";
        return RedirectToAction("MyAssignments");
    }

    // Tải file bài nộp (gia sư hoặc chính học viên đó).
    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
        var uid = _userManager.GetUserId(User)!;
        var a = await _db.Assignments.FirstOrDefaultAsync(x => x.Id == id);
        if (a == null || (a.TutorId != uid && a.StudentId != uid)) return Forbid();
        if (string.IsNullOrEmpty(a.SubmissionFileUrl)) return NotFound();

        var signed = _cloudinary.GetSignedDeliveryUrl(a.SubmissionFileUrl);
        return Redirect(signed);
    }
}
