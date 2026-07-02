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
    private readonly XpService _xpService; //  Khai báo XpService
    private readonly NotificationService _notif;

    public BookingController(
        AppDbContext db,
        UserManager<AppUser> userManager,
        BadgeService badgeService,
        IHubContext<ChatHub> hubContext,
        XpService xpService, //  Inject XpService
        NotificationService notif)
    {
        _db = db;
        _userManager = userManager;
        _badgeService = badgeService;
        _hubContext = hubContext;
        _xpService = xpService; //  Gán XpService
        _notif = notif;
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

        //  Chỉ cho đặt lịch với gia sư đã được duyệt
        var approvedTutor = await _db.TutorProfiles.FindAsync(tutorProfileId);
        if (approvedTutor == null || !approvedTutor.IsApproved)
        {
            TempData["Error"] = "Gia sư này chưa được duyệt hoặc không tồn tại.";
            return RedirectToAction("Search", "TutorSearch");
        }

        //  SỬA LỖI POSTGRESQL: Chuyển thời gian sang UTC trước khi xử lý
        var startUtc = startTime.ToUniversalTime();
        var endUtc = endTime.ToUniversalTime();

        //  Validate thời gian hợp lệ
        if (startUtc >= endUtc)
        {
            TempData["Error"] = "Thời gian kết thúc phải sau thời gian bắt đầu.";
            return RedirectToAction("Create", new { tutorId = tutorProfileId });
        }

        if (startUtc < DateTime.UtcNow.AddHours(1))
        {
            TempData["Error"] = "Vui lòng đặt lịch trước ít nhất 1 tiếng.";
            return RedirectToAction("Create", new { tutorId = tutorProfileId });
        }

        //  Kiểm tra trùng lịch phía server (Gia sư)
        var conflict = await _db.Bookings.AnyAsync(b =>
            b.TutorProfileId == tutorProfileId &&
            (b.Status == "Confirmed" || b.Status == "Pending") &&
            b.StartTime < endUtc && b.EndTime > startUtc);

        if (conflict)
        {
            TempData["Error"] = "Gia sư đã có lịch trong khung giờ này. Vui lòng chọn giờ khác.";
            return RedirectToAction("Create", new { tutorId = tutorProfileId });
        }

        //  Kiểm tra học viên tự trùng lịch với chính mình
        var selfConflict = await _db.Bookings.AnyAsync(b =>
            b.StudentId == user!.Id &&
            (b.Status == "Confirmed" || b.Status == "Pending") &&
            b.StartTime < endUtc && b.EndTime > startUtc);

        if (selfConflict)
        {
            TempData["Error"] = "Bạn đã có lịch học khác trong khung giờ này!";
            return RedirectToAction("Create", new { tutorId = tutorProfileId });
        }

        //  Kiểm tra buổi học nằm trong "Lịch rảnh"gia sư đã đăng ký
        if (!await IsWithinAvailabilityAsync(tutorProfileId, startUtc, endUtc))
        {
            TempData["Error"] = "Gia sư không rảnh trong khung giờ này. Vui lòng chọn giờ nằm trong lịch rảnh của gia sư.";
            return RedirectToAction("Create", new { tutorId = tutorProfileId });
        }

        var booking = new Booking
        {
            StudentId = user!.Id,
            TutorProfileId = tutorProfileId,
            SubjectId = subjectId,
            StartTime = startUtc, // Lưu bản UTC
            EndTime = endUtc,     // Lưu bản UTC
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
            "Yêu cầu học mới",
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

        //  SỬA LỖI HIỂN THỊ: Chuyển lại Local Time khi view
        foreach (var b in bookings)
        {
            b.StartTime = b.StartTime.ToLocalTime();
            b.EndTime = b.EndTime.ToLocalTime();
        }

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

        //  SỬA LỖI HIỂN THỊ: Chuyển lại Local Time khi view
        foreach (var b in bookings)
        {
            b.StartTime = b.StartTime.ToLocalTime();
            b.EndTime = b.EndTime.ToLocalTime();
        }

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

        //  Chỉ gia sư sở hữu booking mới được xác nhận/từ chối (chặn IDOR)
        var currentUserId = _userManager.GetUserId(User);
        if (booking.TutorProfile?.UserId != currentUserId) return Forbid();

        //  Chỉ xử lý yêu cầu đang chờ, tránh ghi đè trạng thái đã xử lý
        if (booking.Status != "Pending")
        {
            TempData["Error"] = "Yêu cầu này đã được xử lý trước đó.";
            return RedirectToAction("TutorRequests");
        }

        booking.Status = status;

        //  Tự động tạo Jitsi room ID khi Confirm
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
            ? "Lịch học đã được xác nhận!"
            : "Lịch học bị từ chối.";
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
        var currentUserId = _userManager.GetUserId(User);
        var booking = await _db.Bookings
            .Include(b => b.TutorProfile)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (booking == null) return NotFound();

        //  Chỉ gia sư sở hữu booking mới được đánh dấu hoàn thành (chặn IDOR)
        if (booking.TutorProfile?.UserId != currentUserId) return Forbid();

        //  Chỉ hoàn thành buổi đã xác nhận, tránh hoàn thành 2 lần → cộng XP lặp
        if (booking.Status != "Confirmed")
        {
            TempData["Error"] = "Chỉ buổi học đã xác nhận mới được đánh dấu hoàn thành.";
            return RedirectToAction("TutorRequests");
        }

        booking.Status = "Completed";
        _db.Notifications.Add(new Notification
        {
            UserId = booking.StudentId,
            Title = "Mời đánh giá buổi học",
            Content = "Buổi học đã kết thúc. Hãy để lại đánh giá cho gia sư nhé!",
            Link = $"/Review/Create?bookingId={booking.Id}"
        });
        await _db.SaveChangesAsync();

        //  Thêm XP vào các hành động hiện có (Bước 3)
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
            "Buổi học hoàn thành!",
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

        // Cho hủy khi Pending hoặc Confirmed (và buổi học chưa bắt đầu)
        if (booking.Status != "Pending" && booking.Status != "Confirmed")
        {
            TempData["Error"] = "Chỉ có thể hủy lịch đang chờ hoặc đã xác nhận.";
            return RedirectToAction("MyBookings");
        }
        if (booking.StartTime <= DateTime.UtcNow)
        {
            TempData["Error"] = "Không thể hủy buổi học đã bắt đầu.";
            return RedirectToAction("MyBookings");
        }

        booking.Status = "Cancelled";

        // Hoàn tiền nếu đã thanh toán
        if (booking.IsPaid)
        {
            if (booking.PaidByPackagePurchaseId.HasValue)
            {
                // Trả lại 1 buổi vào gói
                var purchase = await _db.PackagePurchases.FindAsync(booking.PaidByPackagePurchaseId.Value);
                if (purchase != null)
                {
                    purchase.RemainingSessions++;
                    if (purchase.Status == "Used") purchase.Status = "Active";
                }
                booking.PaidByPackagePurchaseId = null;
                TempData["Success"] = "Đã hủy lịch và hoàn lại 1 buổi vào gói của bạn.";
            }
            else
            {
                // Đánh dấu giao dịch VNPay là đã hoàn (sandbox: không chuyển tiền thật)
                var payment = await _db.Payments
                    .Where(p => p.BookingId == booking.Id && p.Status == "Paid")
                    .OrderByDescending(p => p.PaidAt)
                    .FirstOrDefaultAsync();
                if (payment != null) payment.Status = "Refunded";
                TempData["Success"] = "Đã hủy lịch. Yêu cầu hoàn tiền sẽ được xử lý (môi trường sandbox).";
            }
            booking.IsPaid = false;
        }
        else
        {
            TempData["Success"] = "Đã hủy lịch học.";
        }

        await _db.SaveChangesAsync();

        // Báo cho gia sư
        var tutorProfile = await _db.TutorProfiles.FindAsync(booking.TutorProfileId);
        if (tutorProfile != null)
            await _notif.NotifyAsync(tutorProfile.UserId, "Lịch học bị hủy",
                $"{user.FullName} đã hủy một buổi học.", "/Booking/TutorRequests");

        return RedirectToAction("MyBookings");
    }

    //  Gia sư huỷ buổi học (việc đột xuất) — hoàn tiền/buổi cho học viên
    [Authorize(Roles = "Tutor")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TutorCancel(int id, string? reason)
    {
        var uid = _userManager.GetUserId(User);
        var booking = await _db.Bookings
            .Include(b => b.TutorProfile)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (booking == null) return NotFound();

        if (booking.TutorProfile?.UserId != uid) return Forbid();

        if (booking.Status != "Pending" && booking.Status != "Confirmed")
        {
            TempData["Error"] = "Chỉ huỷ được buổi đang chờ hoặc đã xác nhận.";
            return RedirectToAction("TutorRequests");
        }
        if (booking.StartTime <= DateTime.UtcNow)
        {
            TempData["Error"] = "Không thể huỷ buổi học đã bắt đầu.";
            return RedirectToAction("TutorRequests");
        }

        booking.Status = "Cancelled";

        // Hoàn tiền/buổi cho HỌC VIÊN nếu đã thanh toán
        if (booking.IsPaid)
        {
            if (booking.PaidByPackagePurchaseId.HasValue)
            {
                var purchase = await _db.PackagePurchases.FindAsync(booking.PaidByPackagePurchaseId.Value);
                if (purchase != null)
                {
                    purchase.RemainingSessions++;
                    if (purchase.Status == "Used") purchase.Status = "Active";
                }
                booking.PaidByPackagePurchaseId = null;
            }
            else
            {
                var payment = await _db.Payments
                    .Where(p => p.BookingId == booking.Id && p.Status == "Paid")
                    .OrderByDescending(p => p.PaidAt)
                    .FirstOrDefaultAsync();
                if (payment != null) payment.Status = "Refunded";
            }
            booking.IsPaid = false;
        }

        await _db.SaveChangesAsync();

        var reasonTxt = string.IsNullOrWhiteSpace(reason) ? "" : $"Lý do: {reason}";
        await _notif.NotifyAsync(booking.StudentId, "Gia sư đã huỷ buổi học",
            $"Gia sư đã huỷ một buổi học của bạn.{reasonTxt} Tiền/buổi trong gói (nếu có) đã được hoàn.",
            "/Booking/MyBookings");

        TempData["Success"] = "Đã huỷ buổi học và hoàn tiền/buổi cho học viên (nếu có).";
        return RedirectToAction("TutorRequests");
    }

    // Trang lịch
    [Authorize]
    public IActionResult Calendar() => View();    // API trả JSON cho FullCalendar
    [HttpGet, Authorize]
    public async Task<IActionResult> GetCalendarEvents(DateTime start, DateTime end)
    {
        var userId = _userManager.GetUserId(User);
        List<Booking> bookings;

        // Mở rộng ±2 ngày để tránh lệch timezone
        var startUtc = start.AddDays(-2);
        var endUtc = end.AddDays(2);

        if (User.IsInRole("Tutor"))
        {
            var profile = await _db.TutorProfiles.FirstOrDefaultAsync(t => t.UserId == userId);
            bookings = profile == null ? new() : await _db.Bookings
                .Include(b => b.Student).Include(b => b.Subject)
                .Where(b => b.TutorProfileId == profile.Id && b.StartTime >= startUtc && b.StartTime <= endUtc)
                .ToListAsync();
        }
        else
        {
            bookings = await _db.Bookings
                .Include(b => b.TutorProfile).ThenInclude(t => t.User)
                .Include(b => b.Subject)
                .Where(b => b.StudentId == userId && b.StartTime >= startUtc && b.StartTime <= endUtc)
                .ToListAsync();
        }

        var events = bookings.Select(b => new
        {
            id = b.Id,
            title = User.IsInRole("Tutor")
                      ? $" {b.Subject?.Name} — {b.Student?.FullName}"
                      : $" {b.Subject?.Name} — GS: {b.TutorProfile?.User?.FullName}",
            //  SỬA LỖI POSTGRESQL: Ép lại Local Time cho FullCalendar
            start = b.StartTime.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:ss"),
            end = b.EndTime.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:ss"),
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
        //  SỬA LỖI POSTGRESQL
        var dayStartLocal = date.Date;
        var dayEndLocal = dayStartLocal.AddDays(1);

        var dayStartUtc = dayStartLocal.ToUniversalTime();
        var dayEndUtc = dayEndLocal.ToUniversalTime();

        // Lấy các booking đã Confirmed hoặc Pending trong ngày đó
        var busyBookings = await _db.Bookings
            .Where(b => b.TutorProfileId == tutorProfileId &&
                        b.StartTime >= dayStartUtc && b.StartTime < dayEndUtc &&
                        (b.Status == "Confirmed" || b.Status == "Pending"))
            .Select(b => new {
                //  SỬA LỖI: Render lại theo Local Time cho UI
                start = b.StartTime.ToLocalTime().ToString("HH:mm"),
                end = b.EndTime.ToLocalTime().ToString("HH:mm")
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
        //  SỬA LỖI POSTGRESQL
        var startUtc = start.ToUniversalTime();
        var endUtc = end.ToUniversalTime();

        // Kiểm tra trùng lịch (overlap logic)
        var conflict = await _db.Bookings
            .AnyAsync(b => b.TutorProfileId == tutorProfileId &&
                           (b.Status == "Confirmed" || b.Status == "Pending") &&
                           b.StartTime < endUtc && b.EndTime > startUtc);

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

        //  SỬA LỖI POSTGRESQL: Dùng biến UtcNow
        var nowUtc = DateTime.UtcNow;
        var soonUtc = nowUtc.AddMinutes(30);

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
                    b.StartTime >= nowUtc && b.StartTime <= soonUtc);

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
                        b.StartTime >= nowUtc && b.StartTime <= soonUtc);

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
            //  SỬA LỖI: Trả về Local Time cho Jitsi hiển thị
            startTime = upcoming.StartTime.ToLocalTime().ToString("HH:mm"),
            joinUrl = $"https://meet.jit.si/{upcoming.MeetingRoomId}"
        });
    }

    //  Kiểm tra buổi học có nằm trong lịch rảnh của gia sư không (giờ VN = UTC+7).
    private async Task<bool> IsWithinAvailabilityAsync(int tutorProfileId, DateTime startUtc, DateTime endUtc)
    {
        var avails = await _db.TutorAvailabilities
            .Where(a => a.TutorProfileId == tutorProfileId)
            .ToListAsync();

        // Gia sư chưa đăng ký lịch rảnh -> cho đặt (tương thích dữ liệu cũ).
        if (avails.Count == 0) return true;

        var startLocal = startUtc.AddHours(7);
        var endLocal = endUtc.AddHours(7);
        if (startLocal.Date != endLocal.Date) return false; // không cho vắt qua nửa đêm

        var dow = startLocal.DayOfWeek;
        var s = startLocal.TimeOfDay;
        var e = endLocal.TimeOfDay;
        return avails.Any(a => a.DayOfWeek == dow && a.StartTime <= s && a.EndTime >= e);
    }

    // ===== ĐỔI GIỜ BUỔI HỌC (RESCHEDULE) =====

    [Authorize(Roles = "Student")]
    [HttpGet]
    public async Task<IActionResult> Reschedule(int id)
    {
        var uid = _userManager.GetUserId(User);
        var booking = await _db.Bookings
            .Include(b => b.Subject)
            .Include(b => b.TutorProfile).ThenInclude(t => t.User)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null) return NotFound();
        if (booking.StudentId != uid) return Forbid();
        if (booking.Status != "Pending" && booking.Status != "Confirmed")
        {
            TempData["Error"] = "Chỉ đổi được giờ của buổi đang chờ hoặc đã xác nhận.";
            return RedirectToAction("MyBookings");
        }
        if (booking.StartTime <= DateTime.UtcNow)
        {
            TempData["Error"] = "Không thể đổi giờ buổi học đã bắt đầu.";
            return RedirectToAction("MyBookings");
        }

        return View(booking);
    }

    [Authorize(Roles = "Student")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reschedule(int id, DateTime startTime, DateTime endTime)
    {
        var uid = _userManager.GetUserId(User);
        var booking = await _db.Bookings
            .Include(b => b.TutorProfile)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null) return NotFound();
        if (booking.StudentId != uid) return Forbid();
        if (booking.Status != "Pending" && booking.Status != "Confirmed")
        {
            TempData["Error"] = "Chỉ đổi được giờ của buổi đang chờ hoặc đã xác nhận.";
            return RedirectToAction("MyBookings");
        }
        if (booking.StartTime <= DateTime.UtcNow)
        {
            TempData["Error"] = "Không thể đổi giờ buổi học đã bắt đầu.";
            return RedirectToAction("MyBookings");
        }

        var startUtc = startTime.ToUniversalTime();
        var endUtc = endTime.ToUniversalTime();

        if (startUtc >= endUtc)
        {
            TempData["Error"] = "Thời gian kết thúc phải sau thời gian bắt đầu.";
            return RedirectToAction("Reschedule", new { id });
        }
        if (startUtc < DateTime.UtcNow.AddHours(1))
        {
            TempData["Error"] = "Vui lòng đặt lịch trước ít nhất 1 tiếng.";
            return RedirectToAction("Reschedule", new { id });
        }

        // Trùng lịch gia sư (bỏ qua chính booking này)
        var conflict = await _db.Bookings.AnyAsync(b =>
            b.Id != booking.Id &&
            b.TutorProfileId == booking.TutorProfileId &&
            (b.Status == "Confirmed" || b.Status == "Pending") &&
            b.StartTime < endUtc && b.EndTime > startUtc);
        if (conflict)
        {
            TempData["Error"] = "Gia sư đã có lịch trong khung giờ này.";
            return RedirectToAction("Reschedule", new { id });
        }

        // Trùng lịch của chính học viên (bỏ qua chính booking này)
        var selfConflict = await _db.Bookings.AnyAsync(b =>
            b.Id != booking.Id &&
            b.StudentId == uid &&
            (b.Status == "Confirmed" || b.Status == "Pending") &&
            b.StartTime < endUtc && b.EndTime > startUtc);
        if (selfConflict)
        {
            TempData["Error"] = "Bạn đã có lịch học khác trong khung giờ này!";
            return RedirectToAction("Reschedule", new { id });
        }

        if (!await IsWithinAvailabilityAsync(booking.TutorProfileId, startUtc, endUtc))
        {
            TempData["Error"] = "Gia sư không rảnh trong khung giờ này.";
            return RedirectToAction("Reschedule", new { id });
        }

        booking.StartTime = startUtc;
        booking.EndTime = endUtc;
        // Đổi giờ -> cần gia sư xác nhận lại
        booking.Status = "Pending";
        booking.MeetingRoomId = null;
        await _db.SaveChangesAsync();

        var user = await _userManager.GetUserAsync(User);
        await _notif.NotifyAsync(booking.TutorProfile.UserId, "Yêu cầu đổi giờ",
            $"{user?.FullName} đã đổi giờ một buổi học, cần bạn xác nhận lại.", "/Booking/TutorRequests");

        TempData["Success"] = "Đã đổi giờ. Vui lòng chờ gia sư xác nhận lại.";
        return RedirectToAction("MyBookings");
    }
}