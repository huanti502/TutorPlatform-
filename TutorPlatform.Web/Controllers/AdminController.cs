using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.Services;

namespace TutorPlatform.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly ReportService _reportService;

    public AdminController(AppDbContext db, UserManager<AppUser> userManager, ReportService reportService)
    {
        _db = db;
        _userManager = userManager;
        _reportService = reportService;
    }

    public async Task<IActionResult> Index()
    {
        var now = DateTime.UtcNow;
        ViewBag.TotalUsers = await _db.Users.CountAsync();
        ViewBag.TotalStudents = await _db.Users.CountAsync(u => u.Role == "Student");
        ViewBag.TotalTutors = await _db.TutorProfiles.CountAsync();
        ViewBag.PendingTutors = await _db.TutorProfiles.CountAsync(t => !t.IsApproved);
        ViewBag.TotalBookings = await _db.Bookings.CountAsync();
        ViewBag.CompletedBookings = await _db.Bookings.CountAsync(b => b.Status == "Completed");
        ViewBag.TotalReviews = await _db.Reviews.CountAsync();

        // ── FIX: Load vào memory trước rồi GroupBy ──────────────
        var sixMonthsAgo = now.AddMonths(-6);
        var completedBookings = await _db.Bookings
            .Include(b => b.TutorProfile)
            .Where(b => b.Status == "Completed" && b.CreatedAt >= sixMonthsAgo)
            .ToListAsync(); // Load vào memory

        var monthlyData = completedBookings
            .GroupBy(b => new { b.CreatedAt.Year, b.CreatedAt.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Count = g.Count(),
                Revenue = g.Sum(b => (decimal)(b.EndTime - b.StartTime).TotalHours * b.TutorProfile.HourlyRate)
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToList();

        ViewBag.MonthlyLabels = monthlyData.Select(m => $"{m.Month}/{m.Year}").ToList();
        ViewBag.MonthlyRevenue = monthlyData.Select(m => (long)m.Revenue).ToList();
        ViewBag.MonthlyCount = monthlyData.Select(m => m.Count).ToList();

        ViewBag.NewUsers = await _db.Users
            .Where(u => u.CreatedAt >= now.AddDays(-30))
            .OrderByDescending(u => u.CreatedAt).Take(10).ToListAsync();

        // ── FIX: Load vào memory trước rồi OrderBy Average ──────
        var tutorList = await _db.TutorProfiles
            .Include(t => t.User)
            .Include(t => t.ReceivedReviews)
            .Where(t => t.IsApproved)
            .ToListAsync(); // Load vào memory

        ViewBag.TopTutors = tutorList
            .Where(t => t.ReceivedReviews.Any())
            .OrderByDescending(t => t.ReceivedReviews.Average(r => r.Rating))
            .Take(5)
            .ToList();

        return View();
    }

    public async Task<IActionResult> Users(string? search, string? role)
    {
        var query = _db.Users.AsQueryable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(u => u.FullName.Contains(search) || u.Email!.Contains(search));
        if (!string.IsNullOrEmpty(role))
            query = query.Where(u => u.Role == role);

        var users = await query.OrderByDescending(u => u.CreatedAt).Take(50).ToListAsync();
        ViewBag.Search = search;
        ViewBag.Role = role;
        return View(users);
    }

    public async Task<IActionResult> UserDetail(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        var tutorProfile = await _db.TutorProfiles
            .Include(t => t.TutorSubjects).ThenInclude(ts => ts.Subject)
            .Include(t => t.Certificates)
            .Include(t => t.Bookings)
            .Include(t => t.ReceivedReviews)
            .FirstOrDefaultAsync(t => t.UserId == id);

        ViewBag.TutorProfile = tutorProfile;
        ViewBag.BookingCount = tutorProfile != null
            ? tutorProfile.Bookings.Count
            : await _db.Bookings.CountAsync(b => b.StudentId == id);
        ViewBag.ReviewCount = tutorProfile != null
            ? tutorProfile.ReceivedReviews.Count
            : await _db.Reviews.CountAsync(r => r.StudentId == id);

        return View(user);
    }

    [ValidateAntiForgeryToken]
    [HttpPost]
    public async Task<IActionResult> ToggleLock(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        // ✅ Không cho admin tự khoá chính tài khoản đang đăng nhập
        var currentUserId = _userManager.GetUserId(User);
        if (user.Id == currentUserId)
        {
            TempData["Error"] = "Bạn không thể tự khoá tài khoản của chính mình.";
            return RedirectToAction("Users");
        }

        // ✅ Không cho khoá một tài khoản Admin khác
        if (await _userManager.IsInRoleAsync(user, "Admin"))
        {
            TempData["Error"] = "Không thể khoá một tài khoản Admin.";
            return RedirectToAction("Users");
        }

        user.IsLocked = !user.IsLocked;
        await _userManager.UpdateAsync(user);

        if (user.IsLocked)
            await _userManager.UpdateSecurityStampAsync(user);

        TempData[user.IsLocked ? "Success" : "Info"] = user.IsLocked
            ? $"Đã khoá tài khoản {user.Email}."
            : $"Đã mở khoá tài khoản {user.Email}.";

        var referer = Request.Headers["Referer"].ToString();
        return referer.Contains("UserDetail")
            ? RedirectToAction("UserDetail", new { id = userId })
            : RedirectToAction("Users");
    }

    public async Task<IActionResult> PendingTutors()
    {
        var pending = await _db.TutorProfiles
            .Include(t => t.User)
            .Include(t => t.TutorSubjects).ThenInclude(ts => ts.Subject)
            .Include(t => t.Certificates)
            .Where(t => !t.IsApproved)
            .OrderBy(t => t.User.CreatedAt)
            .ToListAsync();
        return View(pending);
    }

    [ValidateAntiForgeryToken]
    [HttpPost]
    public async Task<IActionResult> ApproveTutor(int tutorId)
    {
        var tutor = await _db.TutorProfiles.FindAsync(tutorId);
        if (tutor == null) return NotFound();

        tutor.IsApproved = true;
        await _db.SaveChangesAsync();

        _db.Notifications.Add(new Notification
        {
            UserId = tutor.UserId,
            Title = "Hồ sơ đã được duyệt ✅",
            Content = "Chúc mừng! Hồ sơ gia sư của bạn đã được Admin duyệt.",
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        });
        await _db.SaveChangesAsync();
        return RedirectToAction("PendingTutors");
    }

    [ValidateAntiForgeryToken]
    [HttpPost]
    public async Task<IActionResult> RejectTutor(int tutorId, string reason)
    {
        var tutor = await _db.TutorProfiles
            .Include(t => t.User)
            .Include(t => t.Certificates)
            .Include(t => t.TutorSubjects)
            .FirstOrDefaultAsync(t => t.Id == tutorId);
        if (tutor == null) return NotFound();

        var user = tutor.User;
        _db.Certificates.RemoveRange(tutor.Certificates);
        _db.TutorSubjects.RemoveRange(tutor.TutorSubjects);
        _db.TutorProfiles.Remove(tutor);
        await _db.SaveChangesAsync();

        if (user != null)
            await _userManager.DeleteAsync(user);

        return RedirectToAction("PendingTutors");
    }

    // ── DUYỆT / HUỶ DUYỆT GIA SƯ (dạng toggle, dùng cho nút trong Tutor/Detail) ──
    [ValidateAntiForgeryToken]
    [HttpPost]
    public async Task<IActionResult> ToggleApprove(int id)
    {
        var tutor = await _db.TutorProfiles.FindAsync(id);
        if (tutor == null) return NotFound();

        tutor.IsApproved = !tutor.IsApproved;
        await _db.SaveChangesAsync();

        TempData["Success"] = tutor.IsApproved
            ? "Đã duyệt gia sư."
            : "Đã huỷ duyệt gia sư.";

        // Quay lại trang trước đó (Tutor/Detail) nếu có
        var referer = Request.Headers["Referer"].ToString();
        return !string.IsNullOrEmpty(referer) ? Redirect(referer)
                                              : RedirectToAction("PendingTutors");
    }

    // ── QUẢN LÝ MÔN HỌC (gộp từ Area Dashboard sang) ────────────────
    public async Task<IActionResult> Subjects()
    {
        var subjects = await _db.Subjects.OrderBy(s => s.Name).ToListAsync();
        return View(subjects);
    }

    [ValidateAntiForgeryToken]
    [HttpPost]
    public async Task<IActionResult> AddSubject(string name, string? description, string level)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Tên môn học không được để trống.";
            return RedirectToAction("Subjects");
        }

        _db.Subjects.Add(new Subject
        {
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Level = level,
            IsActive = true
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã thêm môn học.";
        return RedirectToAction("Subjects");
    }

    [ValidateAntiForgeryToken]
    [HttpPost]
    public async Task<IActionResult> ToggleSubject(int id)
    {
        var subject = await _db.Subjects.FindAsync(id);
        if (subject != null)
        {
            subject.IsActive = !subject.IsActive;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("Subjects");
    }

    // ── BÁO CÁO THỐNG KÊ ────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Reports(DateTime? from, DateTime? to)
    {
        // Mặc định: 12 tháng gần nhất
        var toDate = to ?? DateTime.UtcNow.Date;
        var fromDate = from ?? toDate.AddMonths(-12);
        if (fromDate > toDate) (fromDate, toDate) = (toDate, fromDate);

        var data = await _reportService.BuildReportDataAsync(fromDate, toDate);
        ViewBag.From = fromDate;
        ViewBag.To = toDate;
        return View(data);
    }

    [HttpGet]
    public async Task<IActionResult> ExportExcel(DateTime? from, DateTime? to)
    {
        var toDate = to ?? DateTime.UtcNow.Date;
        var fromDate = from ?? toDate.AddMonths(-12);
        if (fromDate > toDate) (fromDate, toDate) = (toDate, fromDate);

        var bytes = await _reportService.ExportExcelAsync(fromDate, toDate);
        var fileName = $"BaoCao_GiaSu_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx";
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    [HttpGet]
    public async Task<IActionResult> StatsApi()
    {
        // FIX: GroupBy với Include → load vào memory trước
        var bookingsByStatus = await _db.Bookings
            .GroupBy(b => b.Status)
            .Select(g => new { status = g.Key, count = g.Count() })
            .ToListAsync();

        var tutorSubjects = await _db.TutorSubjects
            .Include(ts => ts.Subject)
            .ToListAsync(); // Load vào memory

        var subjectPopularity = tutorSubjects
            .GroupBy(ts => ts.Subject?.Name ?? "Khác")
            .Select(g => new { subject = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count).Take(6)
            .ToList();

        return Json(new { bookingsByStatus, subjectPopularity });
    }

    // ==========================================
    // QUẢN LÝ MÃ GIẢM GIÁ (COUPON)
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> Coupons()
    {
        var coupons = await _db.Coupons
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
        return View(coupons);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCoupon(string code, string discountType, decimal discountValue,
        decimal? maxDiscount, decimal minOrder, DateTime? expiresAt, int usageLimit)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            TempData["Error"] = "Vui lòng nhập mã.";
            return RedirectToAction("Coupons");
        }

        var norm = code.Trim().ToUpperInvariant();
        if (await _db.Coupons.AnyAsync(c => c.Code == norm))
        {
            TempData["Error"] = $"Mã {norm} đã tồn tại.";
            return RedirectToAction("Coupons");
        }

        _db.Coupons.Add(new Coupon
        {
            Code = norm,
            DiscountType = discountType == "Percent" ? "Percent" : "Amount",
            DiscountValue = discountValue,
            MaxDiscount = maxDiscount,
            MinOrder = minOrder,
            // DB lưu UTC; input datetime-local là giờ VN -> trừ 7h
            ExpiresAt = expiresAt.HasValue
                ? DateTime.SpecifyKind(expiresAt.Value.AddHours(-7), DateTimeKind.Utc)
                : null,
            UsageLimit = usageLimit,
            IsActive = true
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Đã tạo mã {norm}.";
        return RedirectToAction("Coupons");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCoupon(int id)
    {
        var c = await _db.Coupons.FindAsync(id);
        if (c != null)
        {
            c.IsActive = !c.IsActive;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("Coupons");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCoupon(int id)
    {
        var c = await _db.Coupons.FindAsync(id);
        if (c != null)
        {
            _db.Coupons.Remove(c);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("Coupons");
    }
}
