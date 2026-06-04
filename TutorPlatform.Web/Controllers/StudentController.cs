using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data; // Thêm để sử dụng AppDbContext
using TutorPlatform.Web.Services;       // Thêm để sử dụng AIService
using TutorPlatform.Web.ViewModels;

namespace TutorPlatform.Web.Controllers;

[Authorize(Roles = "Student")] // Chỉ cho phép Học viên truy cập
public class StudentController : Controller
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IWebHostEnvironment _env;
    private readonly AppDbContext _db; // Bổ sung AppDbContext để truy vấn lịch học & review
    private readonly AIService _ai;    // Bổ sung AIService
    private readonly CloudinaryService _cloudinary; // Lưu ảnh lên Cloudinary (không mất khi redeploy)

    // Cập nhật Constructor để tiêm đầy đủ các dịch vụ
    public StudentController(
        UserManager<AppUser> userManager,
        IWebHostEnvironment env,
        AppDbContext db,
        AIService ai,
        CloudinaryService cloudinary)
    {
        _userManager = userManager;
        _env = env;
        _db = db;
        _ai = ai;
        _cloudinary = cloudinary;
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var model = new StudentProfileViewModel
        {
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Address = user.Address,
            AvatarUrl = user.AvatarUrl
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(StudentProfileViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        // ✅ Xử lý Upload Avatar — lưu lên Cloudinary (KHÔNG lưu local vì Render xóa đĩa mỗi lần deploy)
        if (model.AvatarFile != null && model.AvatarFile.Length > 0)
        {
            var avatarUrl = await _cloudinary.UploadImageAsync(model.AvatarFile, "avatars");
            if (!string.IsNullOrEmpty(avatarUrl))
            {
                user.AvatarUrl = avatarUrl;
            }
        }

        user.FullName = model.FullName;
        user.PhoneNumber = model.PhoneNumber;
        user.Address = model.Address;

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            TempData["Success"] = "Cập nhật hồ sơ thành công!";
            return RedirectToAction(nameof(Profile));
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError("", error.Description);

        model.AvatarUrl = user.AvatarUrl;
        return View(model);
    }

    // ===== BỔ SUNG: HÀNH ĐỘNG AI REPORT PHÂN TÍCH TIẾN ĐỘ HỌC TẬP =====
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> AIReport()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var bookings = await _db.Bookings
            .Include(b => b.Subject)
            .Include(b => b.TutorProfile).ThenInclude(t => t.User)
            .Include(b => b.TutorProfile).ThenInclude(t => t.ReceivedReviews)
            .Where(b => b.StudentId == user!.Id)
            .OrderBy(b => b.StartTime)
            .ToListAsync();

        var reviews = await _db.Reviews
            .Include(r => r.TutorProfile).ThenInclude(t => t.User)
            .Where(r => r.StudentId == user!.Id)
            .ToListAsync();

        if (!bookings.Any())
        {
            ViewBag.NoData = true;
            return View();
        }

        // Thống kê dữ liệu lịch học
        var completed = bookings.Where(b => b.Status == "Completed").ToList();
        var totalHours = completed.Sum(b => (b.EndTime - b.StartTime).TotalHours);
        var subjectGroups = completed
            .GroupBy(b => b.Subject?.Name ?? "Khác")
            .Select(g => new { Subject = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToList();

        var avgRatingGiven = reviews.Any() ? reviews.Average(r => r.Rating) : 0;

        // Cấu trúc chuỗi text cung cấp cho AI phân tích dữ liệu học viên
        var dataForAI = $"""
            HỌC VIÊN: {user!.FullName}
            THỜI GIAN PHÂN TÍCH: {DateTime.Now:dd/MM/yyyy}

            THỐNG KÊ TỔNG QUAN:
            - Tổng số buổi đặt lịch: {bookings.Count}
            - Đã hoàn thành: {completed.Count} buổi ({totalHours:F1} giờ)
            - Đang chờ/Xác nhận: {bookings.Count(b => b.Status is "Pending" or "Confirmed")}
            - Bị hủy/Từ chối: {bookings.Count(b => b.Status is "Cancelled" or "Rejected")}
            - Điểm đánh giá TB cho gia sư: {avgRatingGiven:F1}/5
            - Số gia sư đã học: {bookings.Select(b => b.TutorProfileId).Distinct().Count()}

            MÔN HỌC ĐÃ HỌC:
            {string.Join("\n", subjectGroups.Select(s => $"- {s.Subject}: {s.Count} buổi"))}

            LỊCH HỌC GẦN ĐÂY (5 buổi cuối):
            {string.Join("\n", completed.TakeLast(5).Select(b => $"- {b.StartTime:dd/MM}: {b.Subject?.Name} với {b.TutorProfile?.User?.FullName} ({(b.EndTime - b.StartTime).TotalHours:F1}h)"))}

            CÁC GIA SƯ ĐÃ HỌC & ĐÁNH GIÁ:
            {string.Join("\n", reviews.Select(r => $"- {r.TutorProfile?.User?.FullName}: {r.Rating}/5 sao — {r.Comment?.Substring(0, Math.Min(50, r.Comment?.Length ?? 0))}"))}
            """;

        var systemPrompt = """
            Bạn là chuyên gia giáo dục AI của TutorPlatform.
            Nhiệm vụ: Phân tích dữ liệu học tập của học viên và tạo báo cáo tiến độ chi tiết.

            Báo cáo gồm các phần (dùng emoji và định dạng rõ ràng):

            📊 TỔNG KẾT TIẾN ĐỘ
            Nhận xét tổng quan về hành trình học tập: tích cực hay cần cải thiện,
            đề cập số buổi, thời gian, sự đều đặn.

            💪 ĐIỂM MẠNH
            2-3 điểm tích cực phát hiện từ dữ liệu
            (ví dụ: chăm chỉ, đa dạng môn học, đánh giá tốt gia sư...)

            ⚠️ ĐIỂM CẦN CẢI THIỆN
            2-3 điểm có thể làm tốt hơn
            (ví dụ: tần suất học chưa đều, chưa đánh giá gia sư, ít môn học...)

            🎯 KẾ HOẠCH TUẦN TỚI
            3 gợi ý cụ thể, thực tế cho tuần tới

            ⭐ ĐÁNH GIÁ TỔNG THỂ
            Cho điểm từ 1-10 kèm nhận xét ngắn và lời động viên

            Viết bằng tiếng Việt, thân thiện như gia sư riêng, ngắn gọn và động viên.
            """;

        try
        {
            var report = await _ai.ChatAsync(systemPrompt, dataForAI, maxTokens: 1500);
            ViewBag.Report = report;
            ViewBag.Stats = new
            {
                Total = bookings.Count,
                Completed = completed.Count,
                Hours = totalHours,
                Subjects = subjectGroups,
                AvgRating = avgRatingGiven
            };
        }
        catch
        {
            ViewBag.Error = "AI đang bận, vui lòng thử lại sau vài phút.";
        }

        return View();
    }
}