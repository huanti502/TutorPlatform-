using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;

namespace TutorPlatform.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public AdminController(AppDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
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

        var sixMonthsAgo = now.AddMonths(-6);
        var monthlyData = await _db.Bookings
            .Include(b => b.TutorProfile)
            .Where(b => b.Status == "Completed" && b.CreatedAt >= sixMonthsAgo)
            .GroupBy(b => new { b.CreatedAt.Year, b.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count(), Revenue = g.Sum(b => b.TutorProfile.HourlyRate) })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync();

        ViewBag.MonthlyLabels = monthlyData.Select(m => $"{m.Month}/{m.Year}").ToList();
        ViewBag.MonthlyRevenue = monthlyData.Select(m => (long)m.Revenue).ToList();
        ViewBag.MonthlyCount = monthlyData.Select(m => m.Count).ToList();

        var newUsers = await _db.Users
            .Where(u => u.CreatedAt >= now.AddDays(-30))
            .OrderByDescending(u => u.CreatedAt).Take(10).ToListAsync();
        ViewBag.NewUsers = newUsers;

        var topTutors = await _db.TutorProfiles
            .Include(t => t.User).Include(t => t.ReceivedReviews)
            .Where(t => t.IsApproved && t.ReceivedReviews.Any())
            .OrderByDescending(t => t.ReceivedReviews.Average(r => r.Rating))
            .Take(5).ToListAsync();
        ViewBag.TopTutors = topTutors;

        return View();
    }

    // GET /Admin/Users
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

    // GET /Admin/UserDetail/{id}
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

        var bookingCount = tutorProfile != null
            ? tutorProfile.Bookings.Count
            : await _db.Bookings.CountAsync(b => b.StudentId == id);

        var reviewCount = tutorProfile != null
            ? tutorProfile.ReceivedReviews.Count
            : await _db.Reviews.CountAsync(r => r.StudentId == id);

        ViewBag.TutorProfile = tutorProfile;
        ViewBag.BookingCount = bookingCount;
        ViewBag.ReviewCount = reviewCount;
        return View(user);
    }

    // POST /Admin/ToggleLock
    [ValidateAntiForgeryToken]
    [HttpPost]
    public async Task<IActionResult> ToggleLock(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        user.IsLocked = !user.IsLocked;
        await _userManager.UpdateAsync(user);

        // Nếu đang khóa → đăng xuất ngay lập tức bằng cách đặt Security Stamp mới
        // (buộc tất cả cookie/session hiện tại của user đó vô hiệu hóa)
        if (user.IsLocked)
            await _userManager.UpdateSecurityStampAsync(user);

        TempData[user.IsLocked ? "Success" : "Info"] =
            user.IsLocked
                ? $"Đã khoá tài khoản {user.Email}. Người dùng sẽ bị đăng xuất ngay."
                : $"Đã mở khoá tài khoản {user.Email}.";

        // Quay lại trang chi tiết nếu có referer là UserDetail, không thì về Users
        var referer = Request.Headers["Referer"].ToString();
        if (referer.Contains("UserDetail"))
            return RedirectToAction("UserDetail", new { id = userId });

        return RedirectToAction("Users");
    }

    // GET /Admin/PendingTutors
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

    // POST /Admin/ApproveTutor
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
            Content = "Chúc mừng! Hồ sơ gia sư của bạn đã được Admin duyệt. Bạn có thể nhận học viên ngay bây giờ.",
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        });
        await _db.SaveChangesAsync();
        return RedirectToAction("PendingTutors");
    }

    // POST /Admin/RejectTutor
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

    // GET /Admin/Stats/Api
    [HttpGet]
    public async Task<IActionResult> StatsApi()
    {
        var bookingsByStatus = await _db.Bookings
            .GroupBy(b => b.Status)
            .Select(g => new { status = g.Key, count = g.Count() })
            .ToListAsync();

        var subjectPopularity = await _db.TutorSubjects
            .Include(ts => ts.Subject)
            .GroupBy(ts => ts.Subject!.Name)
            .Select(g => new { subject = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count).Take(6)
            .ToListAsync();

        return Json(new { bookingsByStatus, subjectPopularity });
    }
}