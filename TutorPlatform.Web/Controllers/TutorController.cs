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
    private readonly IWebHostEnvironment _env;
    private readonly TutorPlatform.Web.Services.CloudinaryService _cloudinary;

    public TutorController(AppDbContext db, UserManager<AppUser> userManager, IWebHostEnvironment env, TutorPlatform.Web.Services.CloudinaryService cloudinary)
    {
        _db = db;
        _userManager = userManager;
        _env = env;
        _cloudinary = cloudinary;
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Index(string? keyword, int? subjectId, string? area, decimal? maxRate)
    {
        var query = _db.TutorProfiles
            .AsNoTracking() // 🚀 Tối ưu
            .AsSplitQuery() // 🚀 Tối ưu
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
            .AsNoTracking() // 🚀 Tối ưu
            .AsSplitQuery() // 🚀 Tối ưu
            .Include(t => t.User)
            .Include(t => t.TutorSubjects).ThenInclude(ts => ts.Subject)
            .Include(t => t.ReceivedReviews).ThenInclude(r => r.Student)
            .Include(t => t.Availabilities)
            .Include(t => t.TutorBadges).ThenInclude(tb => tb.Badge)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tutor == null) return NotFound();

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

        ViewBag.Packages = await _db.LessonPackages
            .Where(p => p.TutorProfileId == id && p.IsActive)
            .OrderBy(p => p.Price)
            .ToListAsync();

        return View(tutor);
    }

    // ==========================================
    // ACTION REGISTER (Xử lý Đăng ký & Upload File)
    // ==========================================
    [HttpPost]
    [Authorize(Roles = "Student,Tutor")]
    public async Task<IActionResult> Register(
        RegisterViewModel model,
        IFormFile? avatarFile,
        IFormFile? facePhotoFile,
        List<string>? certTitles,
        List<IFormFile>? certFiles)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");

        // ── Upload Avatar — lưu lên Cloudinary ───────────────
        string? avatarUrl = null;
        if (avatarFile != null && avatarFile.Length > 0)
        {
            avatarUrl = await _cloudinary.UploadImageAsync(avatarFile, "avatars");
            if (!string.IsNullOrEmpty(avatarUrl))
            {
                user.AvatarUrl = avatarUrl;
                await _userManager.UpdateAsync(user);
            }
        }

        // ── Tạo TutorProfile ────────────────────────────────
        var profile = new TutorProfile
        {
            UserId = user.Id,
            IsApproved = false
        };

        _db.TutorProfiles.Add(profile);
        await _db.SaveChangesAsync();

        // ── Upload Ảnh khuôn mặt xác minh — lưu lên Cloudinary ───────────
        if (facePhotoFile != null && facePhotoFile.Length > 0)
        {
            var faceUrl = await _cloudinary.UploadFileAsync(facePhotoFile, "verifications");
            if (!string.IsNullOrEmpty(faceUrl))
            {
                _db.Certificates.Add(new Certificate
                {
                    TutorProfileId = profile.Id,
                    Title = "Ảnh xác minh danh tính",
                    FilePath = faceUrl,
                    FileType = "image",
                    Type = CertificateType.FacePhoto
                });
            }
        }

        // ── Upload Bằng cấp / Chứng chỉ — lưu lên Cloudinary ─────────────
        if (certFiles != null && certTitles != null)
        {
            for (int i = 0; i < Math.Min(certFiles.Count, certTitles.Count); i++)
            {
                var file = certFiles[i];
                var title = certTitles[i];

                if (file == null || file.Length == 0) continue;

                var ext = Path.GetExtension(file.FileName);
                var certUrl = await _cloudinary.UploadFileAsync(file, "certificates");
                if (string.IsNullOrEmpty(certUrl)) continue;

                _db.Certificates.Add(new Certificate
                {
                    TutorProfileId = profile.Id,
                    Title = string.IsNullOrWhiteSpace(title) ? $"Bằng cấp {i + 1}" : title,
                    FilePath = certUrl,
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

        if (profile == null) return View(new TutorProfileFormViewModel
        {
            AvatarUrl = user!.AvatarUrl
        });

        return View(new TutorProfileFormViewModel
        {
            Education = profile.Education,
            ExperienceYears = profile.ExperienceYears,
            TeachingArea = profile.TeachingArea,
            HourlyRate = profile.HourlyRate,
            TeachingMode = profile.TeachingMode,
            Bio = profile.Bio,
            SelectedSubjectIds = profile.TutorSubjects.Select(ts => ts.SubjectId).ToList(),
            AvatarUrl = user!.AvatarUrl
        });
    }

    [Authorize(Roles = "Tutor")]
    [HttpPost]
    public async Task<IActionResult> Profile(TutorProfileFormViewModel model, IFormFile? avatarFile)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Subjects = await _db.Subjects.Where(s => s.IsActive).ToListAsync();
            var currentUser2 = await _userManager.GetUserAsync(User);
            model.AvatarUrl = currentUser2?.AvatarUrl;
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);

        // ── Upload Avatar lên Cloudinary ─────────────────────
        if (avatarFile != null && avatarFile.Length > 0)
        {
            var avatarUrl = await _cloudinary.UploadImageAsync(avatarFile, "avatars");
            if (!string.IsNullOrEmpty(avatarUrl))
            {
                user!.AvatarUrl = avatarUrl;
                await _userManager.UpdateAsync(user);
            }
        }

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
            .AsNoTracking() // 🚀 Tối ưu RAM
            .AsSplitQuery() // 🚀 Tối ưu Database
            .Include(t => t.Bookings).ThenInclude(b => b.Student)
            .Include(t => t.Bookings).ThenInclude(b => b.Subject)
            .Include(t => t.ReceivedReviews).ThenInclude(r => r.Student)
            .Include(t => t.TutorBadges).ThenInclude(tb => tb.Badge)
            .Include(t => t.TutorSubjects).ThenInclude(ts => ts.Subject)
            .FirstOrDefaultAsync(t => t.UserId == user!.Id);

        if (profile == null)
            return RedirectToAction("Profile");

        if (!profile.IsApproved)
            return View("Pending");

        var revenueChart = new List<object>();
        for (int i = 5; i >= 0; i--)
        {
            var m = DateTime.UtcNow.AddMonths(-i);
            var start = new DateTime(m.Year, m.Month, 1, 0, 0, 0, DateTimeKind.Utc);
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

    // ==========================================
    // XÁC THỰC KHUÔN MẶT GIA SƯ (chống giả mạo)
    // ==========================================

    [Authorize(Roles = "Tutor")]
    [HttpGet]
    public async Task<IActionResult> VerifyFace()
    {
        var uid = _userManager.GetUserId(User);
        var profile = await _db.TutorProfiles.FirstOrDefaultAsync(t => t.UserId == uid);
        if (profile == null) return RedirectToAction("Dashboard");

        ViewBag.HasReference = !string.IsNullOrEmpty(profile.FaceDescriptor);
        ViewBag.FaceVerified = profile.FaceVerified;
        ViewBag.FaceVerifiedAt = profile.FaceVerifiedAt;
        return View();
    }

    [Authorize(Roles = "Tutor")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyFace([FromBody] FaceScanDto dto)
    {
        var uid = _userManager.GetUserId(User);
        var profile = await _db.TutorProfiles.FirstOrDefaultAsync(t => t.UserId == uid);
        if (profile == null) return Json(new { ok = false, message = "Không tìm thấy hồ sơ." });

        var live = dto?.Descriptor;
        if (live == null || live.Length < 64)
            return Json(new { ok = false, message = "Không nhận được dữ liệu khuôn mặt hợp lệ." });

        // Lần đầu: ghi danh khuôn mặt gốc.
        if (string.IsNullOrEmpty(profile.FaceDescriptor))
        {
            profile.FaceDescriptor = System.Text.Json.JsonSerializer.Serialize(live);
            profile.FaceVerified = true;
            profile.FaceVerifiedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Json(new { ok = true, enrolled = true, message = "Đã ghi danh khuôn mặt và xác thực thành công." });
        }

        // Các lần sau: so khớp với khuôn mặt gốc (khoảng cách Euclid).
        var reference = System.Text.Json.JsonSerializer.Deserialize<float[]>(profile.FaceDescriptor);
        if (reference == null || reference.Length != live.Length)
            return Json(new { ok = false, message = "Dữ liệu khuôn mặt gốc lỗi." });

        double sum = 0;
        for (int i = 0; i < reference.Length; i++)
        {
            double d = reference[i] - live[i];
            sum += d * d;
        }
        double distance = Math.Sqrt(sum);
        const double threshold = 0.5; // face-api: <0.6 là cùng người; 0.5 chặt hơn

        bool match = distance < threshold;
        profile.FaceVerified = match;
        if (match) profile.FaceVerifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Json(new
        {
            ok = true,
            match,
            distance = Math.Round(distance, 3),
            message = match
                ? "Khuôn mặt khớp — xác thực thành công."
                : "Khuôn mặt KHÔNG khớp với hồ sơ gốc. Có thể không phải chính chủ."
        });
    }

    public class FaceScanDto
    {
        public float[]? Descriptor { get; set; }
    }
}