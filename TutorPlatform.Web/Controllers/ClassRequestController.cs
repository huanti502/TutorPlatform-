using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.Services;

namespace TutorPlatform.Web.Controllers;

// "Lớp cần gia sư": học viên đăng nhu cầu tìm gia sư, gia sư gửi đề nghị dạy.
public class ClassRequestController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly NotificationService _notif;

    public ClassRequestController(AppDbContext db, UserManager<AppUser> userManager, NotificationService notif)
    {
        _db = db;
        _userManager = userManager;
        _notif = notif;
    }

    // GET /ClassRequest — danh sách lớp đang mở (công khai) + bộ lọc
    [HttpGet]
    public async Task<IActionResult> Index(int? subjectId, string? mode, string? keyword, string? sort)
    {
        var query = _db.ClassRequests
            .Include(c => c.Subject)
            .Include(c => c.Applications)
            .Include(c => c.Student)
            .Where(c => c.Status == "Open")
            .AsQueryable();

        if (subjectId.HasValue)
            query = query.Where(c => c.SubjectId == subjectId);
        if (!string.IsNullOrEmpty(mode))
            query = query.Where(c => c.Mode == mode);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim().ToLower();
            query = query.Where(c => c.Title.ToLower().Contains(k)
                || c.Description.ToLower().Contains(k)
                || (c.Location != null && c.Location.ToLower().Contains(k)));
        }

        query = sort switch
        {
            "price_asc" => query.OrderBy(c => c.BudgetPerSession),
            "price_desc" => query.OrderByDescending(c => c.BudgetPerSession),
            _ => query.OrderByDescending(c => c.CreatedAt)
        };

        ViewBag.Subjects = await _db.Subjects.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
        ViewBag.SubjectId = subjectId;
        ViewBag.Mode = mode ?? "";
        ViewBag.Keyword = keyword ?? "";
        ViewBag.Sort = sort ?? "";

        // Nếu là gia sư: đánh dấu lớp đã gửi đề nghị để hiển thị đúng nút.
        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Tutor"))
        {
            var userId = _userManager.GetUserId(User)!;
            var profile = await _db.TutorProfiles.FirstOrDefaultAsync(t => t.UserId == userId);
            if (profile != null)
            {
                ViewBag.MyTutorProfileId = profile.Id;
                ViewBag.AppliedIds = await _db.ClassApplications
                    .Where(a => a.TutorProfileId == profile.Id)
                    .Select(a => a.ClassRequestId)
                    .ToListAsync();
            }
        }

        return View(await query.Take(100).ToListAsync());
    }

    // JSON: số đề nghị hiện tại của các lớp đang mở — dùng để cập nhật động ở trang danh sách.
    [HttpGet]
    public async Task<IActionResult> LiveCounts()
    {
        var data = await _db.ClassRequests
            .Where(c => c.Status == "Open")
            .Select(c => new { id = c.Id, count = c.Applications.Count })
            .ToListAsync();
        return Json(new { total = data.Count, items = data });
    }

    // GET /ClassRequest/Create — form đăng lớp (học viên)
    [Authorize(Roles = "Student")]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.Subjects = await _db.Subjects.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
        return View();
    }

    [Authorize(Roles = "Student")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string title, string description, int? subjectId,
        string mode, string? location, string schedule, int sessionsPerWeek, decimal budgetPerSession)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description)
            || string.IsNullOrWhiteSpace(schedule) || budgetPerSession <= 0)
        {
            TempData["Error"] = "Vui lòng điền đầy đủ tiêu đề, mô tả, lịch học và học phí.";
            return RedirectToAction("Create");
        }

        var userId = _userManager.GetUserId(User)!;

        // Giới hạn: tối đa 5 lớp đang mở / học viên.
        var openCount = await _db.ClassRequests.CountAsync(c => c.StudentId == userId && c.Status == "Open");
        if (openCount >= 5)
        {
            TempData["Error"] = "Bạn đang có 5 lớp mở. Vui lòng đóng bớt trước khi đăng lớp mới.";
            return RedirectToAction("My");
        }

        _db.ClassRequests.Add(new ClassRequest
        {
            StudentId = userId,
            Title = title.Trim(),
            Description = description.Trim(),
            SubjectId = subjectId,
            Mode = mode,
            Location = location?.Trim(),
            Schedule = schedule.Trim(),
            SessionsPerWeek = Math.Clamp(sessionsPerWeek, 1, 7),
            BudgetPerSession = budgetPerSession,
            Status = "Open",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = "Đã đăng lớp! Gia sư phù hợp sẽ gửi đề nghị dạy cho bạn.";
        return RedirectToAction("My");
    }

    // POST /ClassRequest/Apply — gia sư gửi đề nghị dạy
    [Authorize(Roles = "Tutor")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(int classRequestId, string? message, decimal? proposedRate)
    {
        var userId = _userManager.GetUserId(User)!;
        var profile = await _db.TutorProfiles.FirstOrDefaultAsync(t => t.UserId == userId);
        if (profile == null || !profile.IsApproved)
        {
            TempData["Error"] = "Hồ sơ gia sư của bạn cần được duyệt trước khi đề nghị dạy.";
            return RedirectToAction("Index");
        }

        var req = await _db.ClassRequests.FirstOrDefaultAsync(c => c.Id == classRequestId && c.Status == "Open");
        if (req == null)
        {
            TempData["Error"] = "Lớp không tồn tại hoặc đã đóng.";
            return RedirectToAction("Index");
        }
        if (req.StudentId == userId)
        {
            TempData["Error"] = "Bạn không thể đề nghị dạy lớp do chính mình đăng.";
            return RedirectToAction("Index");
        }

        var dup = await _db.ClassApplications
            .AnyAsync(a => a.ClassRequestId == classRequestId && a.TutorProfileId == profile.Id);
        if (dup)
        {
            TempData["Error"] = "Bạn đã gửi đề nghị cho lớp này rồi.";
            return RedirectToAction("Index");
        }

        _db.ClassApplications.Add(new ClassApplication
        {
            ClassRequestId = classRequestId,
            TutorProfileId = profile.Id,
            Message = message?.Trim(),
            ProposedRate = proposedRate,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var tutorName = (await _userManager.FindByIdAsync(userId))?.FullName ?? "Một gia sư";
        await _notif.NotifyAsync(req.StudentId, "Đề nghị dạy mới",
            $"{tutorName} muốn nhận dạy lớp \"{req.Title}\".", "/ClassRequest/My");

        TempData["Success"] = "Đã gửi đề nghị dạy! Học viên sẽ xem xét và phản hồi.";
        return RedirectToAction("Index");
    }

    // GET /ClassRequest/My — lớp của tôi (học viên) kèm các đề nghị
    [Authorize(Roles = "Student")]
    [HttpGet]
    public async Task<IActionResult> My()
    {
        var userId = _userManager.GetUserId(User)!;
        var requests = await _db.ClassRequests
            .Include(c => c.Subject)
            .Include(c => c.Applications).ThenInclude(a => a.TutorProfile).ThenInclude(t => t!.User)
            .Where(c => c.StudentId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
        return View(requests);
    }

    // POST /ClassRequest/Accept — học viên chấp nhận một đề nghị
    [Authorize(Roles = "Student")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(int applicationId)
    {
        var userId = _userManager.GetUserId(User)!;
        var app = await _db.ClassApplications
            .Include(a => a.ClassRequest)
            .Include(a => a.TutorProfile)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        // Kiểm tra quyền sở hữu (chống IDOR).
        if (app?.ClassRequest == null || app.ClassRequest.StudentId != userId || app.ClassRequest.Status != "Open")
        {
            TempData["Error"] = "Đề nghị không hợp lệ.";
            return RedirectToAction("My");
        }

        app.Status = "Accepted";
        app.ClassRequest.Status = "Matched";

        // Từ chối các đề nghị còn lại của lớp này.
        var others = await _db.ClassApplications
            .Where(a => a.ClassRequestId == app.ClassRequestId && a.Id != app.Id && a.Status == "Pending")
            .ToListAsync();
        foreach (var o in others) o.Status = "Rejected";
        await _db.SaveChangesAsync();

        // Báo cho gia sư được chọn (kèm link đặt lịch qua hồ sơ).
        if (app.TutorProfile != null)
        {
            await _notif.NotifyAsync(app.TutorProfile.UserId, "Đề nghị dạy được chấp nhận",
                $"Học viên đã chấp nhận đề nghị của bạn cho lớp \"{app.ClassRequest.Title}\". Hãy nhắn tin để thống nhất buổi học đầu tiên.",
                "/Message");
        }

        TempData["Success"] = "Đã chấp nhận đề nghị! Hãy đặt lịch với gia sư để bắt đầu học.";
        return RedirectToAction("My");
    }

    // POST /ClassRequest/Close — học viên đóng lớp
    [Authorize(Roles = "Student")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(int id)
    {
        var userId = _userManager.GetUserId(User)!;
        var req = await _db.ClassRequests.FirstOrDefaultAsync(c => c.Id == id && c.StudentId == userId);
        if (req == null) return RedirectToAction("My");

        req.Status = "Closed";
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã đóng lớp.";
        return RedirectToAction("My");
    }
}
