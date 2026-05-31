using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.Services;
using TutorPlatform.Web.Hubs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TutorPlatform.Web.Controllers;

[Authorize]
public class BookingController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly BadgeService _badgeService;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly XpService _xpService; // ✅ Khai báo XpService

    public BookingController(
        AppDbContext db,
        UserManager<AppUser> userManager,
        BadgeService badgeService,
        IHubContext<ChatHub> hubContext,
        XpService xpService) // ✅ Inject XpService
    {
        _db = db;
        _userManager = userManager;
        _badgeService = badgeService;
        _hubContext = hubContext;
        _xpService = xpService; // ✅ Gán XpService
    }

    [Authorize(Roles = "Student")]
    [HttpGet]
    public async Task<IActionResult> Create(int tutorId)
    {
        var tutor = await _db.TutorProfiles
            .Include(t => t.User)
            .Include(t => t.TutorSubjects).ThenInclude(ts => ts.Subject)
            .FirstOrDefaultAsync(t => t.Id == tutorId);

        if (tutor == null) return NotFound();

        ViewBag.Tutor = tutor;
        return View();
    }

    [Authorize(Roles = "Student")]
    [HttpPost]
    public async Task<IActionResult> Create(int tutorProfileId, int subjectId,
        DateTime startTime, DateTime endTime, string teachingMode, string? note)
    {
        var user = await _userManager.GetUserAsync(User);

        // ✅ Validate thời gian hợp lệ
        if (startTime >= endTime)
        {
            TempData["Error"] = "Thời gian kết thúc phải sau thời gian bắt đầu.";
            return RedirectToAction("Create", new { tutorId = tutorProfileId });
        }

        if (startTime < DateTime.Now.AddHours(1))
        {
            TempData["Error"] = "Vui lòng đặt lịch trước ít nhất 1 tiếng.";
            return RedirectToAction("Create", new { tutorId = tutorProfileId });
        }

        // ✅ Kiểm tra trùng lịch phía server (Gia sư)
        var conflict = await _db.Bookings.AnyAsync(b =>
            b.TutorProfileId == tutorProfileId &&
            (b.Status == "Confirmed" || b.Status == "Pending") &&
            b.StartTime < endTime && b.EndTime > startTime);

        if (conflict)
        {
            TempData["Error"] = "⚠️ Gia sư đã có lịch trong khung giờ này. Vui lòng chọn giờ khác.";
            return RedirectToAction("Create", new { tutorId = tutorProfileId });
        }

        // ✅ Kiểm tra học viên tự trùng lịch với chính mình
        var selfConflict = await _db.Bookings.AnyAsync(b =>
            b.StudentId == user!.Id &&
            (b.Status == "Confirmed" || b.Status == "Pending") &&
            b.StartTime < endTime && b.EndTime > startTime);

        if (selfConflict)
        {
            TempData["Error"] = "⚠️ Bạn đã có lịch học khác trong khung giờ này!";
            return RedirectToAction("Create", new { tutorId = tutorProfileId });
        }

        var booking = new Booking
        {
            StudentId = user!.Id,
            TutorProfileId = tutorProfileId,
            SubjectId = subjectId,
            StartTime = startTime,
            EndTime = endTime,
            TeachingMode = teachingMode,
            Note = note,
            Status = "Pending"
        };

        _db.Bookings.Add(booking);

        var tutor = await _db.TutorProfiles.FindAsync(tutorProfileId);
        _db.Notifications.Add(new Notification
        {
            UserId = tutor!.UserId,
            Title = "Yêu cầu học mới",
            Content = $"{user.FullName} đã gửi yêu cầu đặt lịch học.",
            Link = "/Booking/TutorRequests"
        });

        await _db.SaveChangesAsync();

        // Push thông báo realtime cho gia sư
        await ChatHub.SendNotificationToUser(
            _hubContext,
            tutor!.UserId,
            "📅 Yêu cầu học mới",
            $"{user.FullName} vừa đặt lịch học với bạn!",
            "/Booking/TutorRequests");

        TempData["Success"] = "Đặt lịch thành công! Vui lòng chờ gia sư xác nhận.";
        return RedirectToAction("MyBookings");
    }

    [Authorize(Roles = "Student")]
    public async Task<IActionResult> MyBookings()
    {
        var user = await _userManager.GetUserAsync(User);
        var bookings = await _db.Bookings
            .Include(b => b.TutorProfile).ThenInclude(t => t.User)
            .Include(b => b.Subject)
            .Where(b => b.StudentId == user!.Id)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        // Lấy danh sách bookingId đã được đánh giá rồi
        var bookingIds = bookings.Select(b => b.Id).ToList();
        var reviewedIds = await _db.Reviews
            .Where(r => bookingIds.Contains(r.BookingId))
            .Select(r => r.BookingId)
            .ToHashSetAsync();

        ViewBag.ReviewedIds = reviewedIds;

        return View(bookings);
    }

    [Authorize(Roles = "Tutor")]
    public async Task<IActionResult> TutorRequests()
    {
        var user = await _userManager.GetUserAsync(User);
        var profile = await _db.TutorProfiles
            .FirstOrDefaultAsync(t => t.UserId == user!.Id);

        if (profile == null)
            return RedirectToAction("Profile", "Tutor");

        var bookings = await _db.Bookings
            .Include(b => b.Student)
            .Include(b => b.Subject)
            .Where(b => b.TutorProfileId == profile.Id)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        return View(bookings);
    }

    [Authorize(Roles = "Tutor")]
    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        var allowedStatuses = new[] { "Confirmed", "Rejected" };
        if (!allowedStatuses.Contains(status)) return BadRequest();

        var booking = await _db.Bookings
            .Include(b => b.Student)
            .Include(b => b.TutorProfile).ThenInclude(t => t.User)
            .Include(b => b.Subject)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null) return NotFound();

        booking.Status = status;

        // ✅ Tự động tạo Jitsi room ID khi Confirm
        if (status == "Confirmed" && string.IsNullOrEmpty(booking.MeetingRoomId))
        {
            booking.MeetingRoomId = $"TutorPlatform-{booking.Id}-{Guid.NewGuid().ToString("N")[..8]}";
        }

        _db.Notifications.Add(new Notification
        {
            UserId = booking.StudentId,
            Title = status == "Confirmed" ? "Lịch học đã được xác nhận" : "Lịch học bị từ chối",
            Content = status == "Confirmed"
                ? "Gia sư đã xác nhận lịch học của bạn."
                : "Gia sư đã từ chối yêu cầu.",
            Link = "/Booking/MyBookings"
        });

        await _db.SaveChangesAsync();

        // Push thông báo realtime cho học viên
        var statusMsg = status == "Confirmed"
            ? "✅ Lịch học đã được xác nhận!"
            : "❌ Lịch học bị từ chối.";
        await ChatHub.SendNotificationToUser(
            _hubContext,
            booking.StudentId,
            statusMsg,
            status == "Confirmed"
                ? "Gia sư đã xác nhận lịch của bạn."
                : "Gia sư đã từ chối yêu cầu.",
            "/Booking/MyBookings");

        return RedirectToAction("TutorRequests");
    }

    [Authorize(Roles = "Tutor")]
    [HttpPost]
    public async Task<IActionResult> Complete(int id)
    {
        var booking = await _db.Bookings.FindAsync(id);
        if (booking == null) return NotFound();

        booking.Status = "Completed";
        _db.Notifications.Add(new Notification
        {
            UserId = booking.StudentId,
            Title = "Buổi học đã hoàn thành",
            Content = "Buổi học đã kết thúc. Hãy để lại đánh giá cho gia sư nhé!",
            Link = "/Booking/MyBookings"
        });
        await _db.SaveChangesAsync();

        // ✅ Thêm XP vào các hành động hiện có (Bước 3)
        await _xpService.AwardXpAsync(booking.StudentId, "booking_completed");

        var isFirst = await _db.Bookings.CountAsync(b => b.StudentId == booking.StudentId && b.Status == "Completed") == 1;
        if (isFirst)
        {
            await _xpService.AwardXpAsync(booking.StudentId, "first_booking");
        }

        // Tự động kiểm tra và trao huy hiệu
        await _badgeService.CheckAndAwardAsync(booking.TutorProfileId);

        // Push thông báo realtime cho học viên
        await ChatHub.SendNotificationToUser(
            _hubContext,
            booking.StudentId,
            "🎉 Buổi học hoàn thành!",
            "Hãy đánh giá gia sư để giúp cộng đồng nhé.",
            "/Booking/MyBookings");

        TempData["Success"] = "Đã đánh dấu hoàn thành!";
        return RedirectToAction("TutorRequests");
    }

    [Authorize(Roles = "Student")]
    [HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var booking = await _db.Bookings.FindAsync(id);
        if (booking == null) return NotFound();

        // Chỉ chủ booking mới được hủy
        if (booking.StudentId != user!.Id) return Forbid();

        // Chỉ cho hủy khi đang Pending
        if (booking.Status != "Pending")
        {
            TempData["Error"] = "Chỉ có thể hủy lịch đang chờ xác nhận.";
            return RedirectToAction("MyBookings");
        }

        booking.Status = "Cancelled";
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã hủy lịch học.";
        return RedirectToAction("MyBookings");
    }

    // Trang lịch
    [Authorize]
    public IActionResult Calendar() => View();

    // API trả JSON cho FullCalendar
    [HttpGet, Authorize]
    public async Task<IActionResult> GetCalendarEvents(DateTime start, DateTime end)
    {
        var userId = _userManager.GetUserId(User);
        List<Booking> bookings;

        if (User.IsInRole("Tutor"))
        {
            var profile = await _db.TutorProfiles.FirstOrDefaultAsync(t => t.UserId == userId);
            bookings = profile == null ? new() : await _db.Bookings
                .Include(b => b.Student).Include(b => b.Subject)
                .Where(b => b.TutorProfileId == profile.Id && b.StartTime >= start && b.EndTime <= end)
                .ToListAsync();
        }
        else
        {
            bookings = await _db.Bookings
                .Include(b => b.TutorProfile).ThenInclude(t => t.User)
                .Include(b => b.Subject)
                .Where(b => b.StudentId == userId && b.StartTime >= start && b.EndTime <= end)
                .ToListAsync();
        }

        var events = bookings.Select(b => new
        {
            id = b.Id,
            title = User.IsInRole("Tutor")
                      ? $"📚 {b.Subject?.Name} — {b.Student?.FullName}"
                      : $"📚 {b.Subject?.Name} — GS: {b.TutorProfile?.User?.FullName}",
            start = b.StartTime.ToString("yyyy-MM-ddTHH:mm:ss"),
            end = b.EndTime.ToString("yyyy-MM-ddTHH:mm:ss"),
            color = b.Status switch
            {
                "Pending" => "#FFA500",
                "Confirmed" => "#2196F3",
                "Completed" => "#4CAF50",
                "Cancelled" => "#9E9E9E",
                "Rejected" => "#F44336",
                _ => "#607D8B"
            },
            extendedProps = new
            {
                status = b.Status,
                teachMode = b.TeachingMode,
                note = b.Note
            }
        });

        return Json(events);
    }

    /// <summary>
    /// API: Trả về các khung giờ bị bận của gia sư trong 1 ngày.
    /// Frontend gọi khi user chọn ngày để disable các giờ đã có người đặt.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetBusySlots(int tutorProfileId, DateTime date)
    {
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);

        // Lấy các booking đã Confirmed hoặc Pending trong ngày đó
        var busyBookings = await _db.Bookings
            .Where(b => b.TutorProfileId == tutorProfileId &&
                        b.StartTime >= dayStart && b.StartTime < dayEnd &&
                        (b.Status == "Confirmed" || b.Status == "Pending"))
            .Select(b => new {
                start = b.StartTime.ToString("HH:mm"),
                end = b.EndTime.ToString("HH:mm")
            })
            .ToListAsync();

        // Lịch trống gia sư đăng ký (TutorAvailability)
        var dayOfWeek = date.DayOfWeek;
        var availability = await _db.TutorAvailabilities
            .Where(a => a.TutorProfileId == tutorProfileId && a.DayOfWeek == dayOfWeek)
            .Select(a => new {
                start = a.StartTime.ToString(@"hh\:mm"),
                end = a.EndTime.ToString(@"hh\:mm")
            })
            .ToListAsync();

        return Json(new { busySlots = busyBookings, availableSlots = availability });
    }

    /// <summary>
    /// API: Kiểm tra 1 khung giờ cụ thể có bị trùng không.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> CheckConflict(int tutorProfileId, DateTime start, DateTime end)
    {
        // Kiểm tra trùng lịch (overlap logic)
        var conflict = await _db.Bookings
            .AnyAsync(b => b.TutorProfileId == tutorProfileId &&
                           (b.Status == "Confirmed" || b.Status == "Pending") &&
                           b.StartTime < end && b.EndTime > start);

        return Json(new { hasConflict = conflict });
    }

    // ==============================================================
    // API kiểm tra buổi học sắp tới trong 30 phút
    // ==============================================================
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetUpcoming()
    {
        var userId = _userManager.GetUserId(User);
        var now = DateTime.Now;
        var soon = now.AddMinutes(30);

        // Tìm booking sắp bắt đầu trong 30 phút
        Booking? upcoming = null;
        string partnerName = "";

        if (User.IsInRole("Student"))
        {
            upcoming = await _db.Bookings
                .Include(b => b.Subject)
                .Include(b => b.TutorProfile).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(b =>
                    b.StudentId == userId &&
                    b.Status == "Confirmed" &&
                    b.StartTime >= now && b.StartTime <= soon);

            partnerName = upcoming?.TutorProfile?.User?.FullName ?? "";
        }
        else if (User.IsInRole("Tutor"))
        {
            var profile = await _db.TutorProfiles
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (profile != null)
            {
                upcoming = await _db.Bookings
                    .Include(b => b.Subject)
                    .Include(b => b.Student)
                    .FirstOrDefaultAsync(b =>
                        b.TutorProfileId == profile.Id &&
                        b.Status == "Confirmed" &&
                        b.StartTime >= now && b.StartTime <= soon);

                partnerName = upcoming?.Student?.FullName ?? "";
            }
        }

        if (upcoming == null)
            return Json(new { hasUpcoming = false });

        return Json(new
        {
            hasUpcoming = true,
            subjectName = upcoming.Subject?.Name,
            partnerName,
            startTime = upcoming.StartTime.ToString("HH:mm"),
            joinUrl = $"https://meet.jit.si/{upcoming.MeetingRoomId}"
        });
    }
}