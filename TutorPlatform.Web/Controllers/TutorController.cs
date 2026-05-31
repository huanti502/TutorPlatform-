using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.ViewModels;

namespace TutorPlatform.Web.Controllers;

public class TutorController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly IWebHostEnvironment _env; // Thêm IWebHostEnvironment để xử lý file

    public TutorController(AppDbContext db, UserManager<AppUser> userManager, IWebHostEnvironment env)
    {
        _db = db;
        _userManager = userManager;
        _env = env;
    }

    public async Task<IActionResult> Index(string? keyword, int? subjectId, string? area, decimal? maxRate)
    {
        var query = _db.TutorProfiles
            .Include(t => t.User)
            .Include(t => t.TutorSubjects).ThenInclude(ts => ts.Subject)
            .Include(t => t.ReceivedReviews)
            .Where(t => t.IsApproved && !t.User.IsLocked)
            .AsQueryable();

        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(t =>
                t.User.FullName.Contains(keyword) ||
                t.TutorSubjects.Any(ts => ts.Subject.Name.Contains(keyword)));

        if (subjectId.HasValue)
            query = query.Where(t => t.TutorSubjects.Any(ts => ts.SubjectId == subjectId));

        if (!string.IsNullOrEmpty(area))
            query = query.Where(t => t.TeachingArea.Contains(area));

        if (maxRate.HasValue)
            query = query.Where(t => t.HourlyRate <= maxRate);

        var tutors = await query.ToListAsync();
        var subjects = await _db.Subjects.Where(s => s.IsActive).ToListAsync();

        ViewBag.Subjects = subjects;
        ViewBag.Keyword = keyword;
        ViewBag.SubjectId = subjectId;

        return View(tutors);
    }

    public async Task<IActionResult> Detail(int id)
    {
        var tutor = await _db.TutorProfiles
            .Include(t => t.User)
            .Include(t => t.TutorSubjects).ThenInclude(ts => ts.Subject)
            .Include(t => t.ReceivedReviews).ThenInclude(r => r.Student)
            .Include(t => t.Availabilities)
            .Include(t => t.TutorBadges).ThenInclude(tb => tb.Badge)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tutor == null) return NotFound();

        // Thêm đoạn code kiểm tra xem user hiện tại đã có booking được xác nhận hay chưa
        var currentUserId = _userManager.GetUserId(User);
        var hasBooking = false;

        if (currentUserId != null)
        {
            hasBooking = await _db.Bookings.AnyAsync(b =>
                b.StudentId == currentUserId &&
                b.TutorProfileId == id &&
                (b.Status == "Confirmed" || b.Status == "Completed"));
        }

        ViewBag.HasConfirmedBooking = hasBooking;
        ViewBag.CurrentUserId = currentUserId;

        return View(tutor);
    }

    // ==========================================
    // ACTION REGISTER (Xử lý Đăng ký & Upload File)
    // ==========================================
    [HttpPost]
    [Authorize(Roles = "Student,Tutor")]
    public async Task<IActionResult> Register(
        RegisterViewModel model, // Thay đổi thành Model tương ứng nếu cần
        IFormFile? avatarFile,
        IFormFile? facePhotoFile,
        List<string>? certTitles,
        List<IFormFile>? certFiles)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");

        var uploadsPath = Path.Combine(_env.WebRootPath, "uploads");

        // ── Upload Avatar ───────────────────────────────────
        string? avatarUrl = null;
        if (avatarFile != null && avatarFile.Length > 0)
        {
            var avatarFolder = Path.Combine(uploadsPath, "avatars");
            Directory.CreateDirectory(avatarFolder);

            var ext = Path.GetExtension(avatarFile.FileName);
            var fileName = $"avatar_{user.Id}{ext}";
            var path = Path.Combine(avatarFolder, fileName);

            using var stream = new FileStream(path, FileMode.Create);
            await avatarFile.CopyToAsync(stream);
            avatarUrl = $"/uploads/avatars/{fileName}";

            // Cập nhật avatar vào AppUser
            user.AvatarUrl = avatarUrl;
            await _userManager.UpdateAsync(user);
        }

        // ── Tạo TutorProfile ────────────────────────────────
        var profile = new TutorProfile
        {
            UserId = user.Id,
            // Ánh xạ các trường khác từ model form nếu có (ví dụ: model.PhoneNumber, model.Bio...)

            IsApproved = false
        };

        _db.TutorProfiles.Add(profile);
        await _db.SaveChangesAsync(); // Phải SaveChanges để lấy ID của profile

        // ── Upload Ảnh khuôn mặt xác minh ────────────────────────────
        if (facePhotoFile != null && facePhotoFile.Length > 0)
        {
            var faceFolder = Path.Combine(uploadsPath, "verifications");
            Directory.CreateDirectory(faceFolder);

            var ext = Path.GetExtension(facePhotoFile.FileName);
            var name = $"face_{profile.Id}_{Guid.NewGuid()}{ext}";
            var path = Path.Combine(faceFolder, name);

            using var stream = new FileStream(path, FileMode.Create);
            await facePhotoFile.CopyToAsync(stream);

            _db.Certificates.Add(new Certificate
            {
                TutorProfileId = profile.Id,
                Title = "Ảnh xác minh danh tính",
                FilePath = $"/uploads/verifications/{name}",
                FileType = "image",
                Type = CertificateType.FacePhoto
            });
        }

        // ── Upload Bằng cấp / Chứng chỉ ─────────────────────────────────
        if (certFiles != null && certTitles != null)
        {
            var certFolder = Path.Combine(uploadsPath, "certificates");
            Directory.CreateDirectory(certFolder);

            for (int i = 0; i < Math.Min(certFiles.Count, certTitles.Count); i++)
            {
                var file = certFiles[i];
                var title = certTitles[i];

                if (file == null || file.Length == 0) continue;

                var ext = Path.GetExtension(file.FileName);
                var name = $"cert_{profile.Id}_{Guid.NewGuid()}{ext}";
                var path = Path.Combine(certFolder, name);

                using var stream = new FileStream(path, FileMode.Create);
                await file.CopyToAsync(stream);

                _db.Certificates.Add(new Certificate
                {
                    TutorProfileId = profile.Id,
                    Title = string.IsNullOrWhiteSpace(title) ? $"Bằng cấp {i + 1}" : title,
                    FilePath = $"/uploads/certificates/{name}",
                    FileType = ext.ToLower() == ".pdf" ? "pdf" : "image",
                    Type = CertificateType.Degree
                });
            }
        }

        await _db.SaveChangesAsync();

        TempData["Success"] = "Đăng ký thành công! Hồ sơ đang chờ Admin duyệt.";
        return RedirectToAction("Dashboard");
    }

    [Authorize(Roles = "Tutor")]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        var profile = await _db.TutorProfiles
            .Include(t => t.TutorSubjects)
            .FirstOrDefaultAsync(t => t.UserId == user!.Id);

        var subjects = await _db.Subjects.Where(s => s.IsActive).ToListAsync();
        ViewBag.Subjects = subjects;

        if (profile == null) return View(new TutorProfileFormViewModel());

        return View(new TutorProfileFormViewModel
        {
            Education = profile.Education,
            ExperienceYears = profile.ExperienceYears,
            TeachingArea = profile.TeachingArea,
            HourlyRate = profile.HourlyRate,
            TeachingMode = profile.TeachingMode,
            Bio = profile.Bio,
            SelectedSubjectIds = profile.TutorSubjects.Select(ts => ts.SubjectId).ToList()
        });
    }

    [Authorize(Roles = "Tutor")]
    [HttpPost]
    public async Task<IActionResult> Profile(TutorProfileFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Subjects = await _db.Subjects.Where(s => s.IsActive).ToListAsync();
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        var profile = await _db.TutorProfiles
            .Include(t => t.TutorSubjects)
            .FirstOrDefaultAsync(t => t.UserId == user!.Id);

        if (profile == null)
        {
            profile = new TutorProfile { UserId = user!.Id };
            _db.TutorProfiles.Add(profile);
            await _db.SaveChangesAsync();
        }

        profile.Education = model.Education;
        profile.ExperienceYears = model.ExperienceYears;
        profile.TeachingArea = model.TeachingArea;
        profile.HourlyRate = model.HourlyRate;
        profile.TeachingMode = model.TeachingMode;
        profile.Bio = model.Bio;

        _db.TutorSubjects.RemoveRange(profile.TutorSubjects);
        foreach (var id in model.SelectedSubjectIds)
        {
            _db.TutorSubjects.Add(new TutorSubject
            {
                TutorProfileId = profile.Id,
                SubjectId = id
            });
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Cập nhật hồ sơ thành công!";
        return RedirectToAction("Profile");
    }

    [Authorize(Roles = "Tutor")]
    [HttpGet]
    public async Task<IActionResult> Availability()
    {
        var user = await _userManager.GetUserAsync(User);
        var profile = await _db.TutorProfiles
            .Include(t => t.Availabilities)
            .FirstOrDefaultAsync(t => t.UserId == user!.Id);

        if (profile == null) return RedirectToAction("Profile");
        if (!profile.IsApproved) return View("Pending");

        return View(profile.Availabilities.OrderBy(a => a.DayOfWeek).ThenBy(a => a.StartTime).ToList());
    }

    [Authorize(Roles = "Tutor")]
    [HttpPost]
    public async Task<IActionResult> AddAvailability(AvailabilityViewModel model)
    {
        if (model.StartTime >= model.EndTime)
        {
            TempData["Error"] = "Giờ bắt đầu phải nhỏ hơn giờ kết thúc.";
            return RedirectToAction("Availability");
        }

        var user = await _userManager.GetUserAsync(User);
        var profile = await _db.TutorProfiles.FirstOrDefaultAsync(t => t.UserId == user!.Id);

        var availability = new TutorAvailability
        {
            TutorProfileId = profile!.Id,
            DayOfWeek = model.DayOfWeek,
            StartTime = model.StartTime,
            EndTime = model.EndTime
        };

        _db.TutorAvailabilities.Add(availability);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã thêm khung giờ rảnh mới.";
        return RedirectToAction("Availability");
    }

    [Authorize(Roles = "Tutor")]
    [HttpPost]
    public async Task<IActionResult> DeleteAvailability(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var profile = await _db.TutorProfiles.FirstOrDefaultAsync(t => t.UserId == user!.Id);

        var availability = await _db.TutorAvailabilities
            .FirstOrDefaultAsync(a => a.Id == id && a.TutorProfileId == profile!.Id);

        if (availability != null)
        {
            _db.TutorAvailabilities.Remove(availability);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Đã xóa khung giờ.";
        }

        return RedirectToAction("Availability");
    }

    // ── Helper: kiểm tra gia sư đã được duyệt chưa ──────────────────
    private async Task<TutorProfile?> GetApprovedProfileAsync(string userId)
    {
        return await _db.TutorProfiles
            .FirstOrDefaultAsync(t => t.UserId == userId && t.IsApproved);
    }

    // ==========================================
    // ACTION DASHBOARD 
    // ==========================================
    [Authorize(Roles = "Tutor")]
    public async Task<IActionResult> Dashboard()
    {
        var user = await _userManager.GetUserAsync(User);
        var profile = await _db.TutorProfiles
            .Include(t => t.Bookings).ThenInclude(b => b.Student)
            .Include(t => t.Bookings).ThenInclude(b => b.Subject)
            .Include(t => t.ReceivedReviews).ThenInclude(r => r.Student)
            .Include(t => t.TutorBadges).ThenInclude(tb => tb.Badge)
            .Include(t => t.TutorSubjects).ThenInclude(ts => ts.Subject)
            .FirstOrDefaultAsync(t => t.UserId == user!.Id);

        // Chưa có hồ sơ nào → trang đăng ký
        if (profile == null)
            return RedirectToAction("Profile");

        // Có hồ sơ nhưng chưa được duyệt → trang chờ duyệt
        if (!profile.IsApproved)
            return View("Pending");

        // Doanh thu 6 tháng cho biểu đồ
        var revenueChart = new List<object>();
        for (int i = 5; i >= 0; i--)
        {
            var m = DateTime.Now.AddMonths(-i);
            var start = new DateTime(m.Year, m.Month, 1);
            var end = start.AddMonths(1);
            var rev = profile.Bookings
                .Where(b => b.Status == "Completed" && b.CreatedAt >= start && b.CreatedAt < end)
                .Sum(b => (decimal)(b.EndTime - b.StartTime).TotalHours * profile.HourlyRate);
            revenueChart.Add(new { label = m.ToString("MM/yyyy"), revenue = rev });
        }

        ViewBag.TotalRevenue = profile.Bookings
            .Where(b => b.Status == "Completed")
            .Sum(b => (decimal)(b.EndTime - b.StartTime).TotalHours * profile.HourlyRate);
        ViewBag.TotalBookings = profile.Bookings.Count;
        ViewBag.ConfirmedCount = profile.Bookings.Count(b => b.Status == "Confirmed");
        ViewBag.PendingCount = profile.Bookings.Count(b => b.Status == "Pending");
        ViewBag.AvgRating = profile.ReceivedReviews.Any()
            ? profile.ReceivedReviews.Average(r => r.Rating) : 0.0;
        ViewBag.TotalReviews = profile.ReceivedReviews.Count;
        ViewBag.RecentBookings = profile.Bookings
            .OrderByDescending(b => b.CreatedAt).Take(8).ToList();
        ViewBag.RevenueChart = System.Text.Json.JsonSerializer.Serialize(revenueChart);

        return View(profile);
    }
}