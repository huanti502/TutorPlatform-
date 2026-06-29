using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;

namespace TutorPlatform.Web.Controllers;

public class TutorSearchController : Controller
{
    private readonly AppDbContext _db;
    public TutorSearchController(AppDbContext db) => _db = db;

    public class SearchResult
    {
        public TutorProfile Profile { get; set; } = null!;
        public double AvgRating { get; set; }
        public int ReviewCount { get; set; }
        public int CompletedCount { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        string? keyword, int? subjectId, string? mode, string? level,   // ✅ thêm "level"
        decimal minPrice = 0, decimal maxPrice = 1_000_000,
        double minRating = 0, string sortBy = "rating", int page = 1)
    {
        const int pageSize = 9;

        var query = _db.TutorProfiles
            .AsNoTracking() // 🚀 TĂNG TỐC: Giải phóng RAM
            .AsSplitQuery() // 🚀 TĂNG TỐC: Chia nhỏ truy vấn chống nghẽn
            .Include(t => t.User)
            .Include(t => t.TutorSubjects).ThenInclude(ts => ts.Subject)
            .Include(t => t.ReceivedReviews)
            .Include(t => t.Bookings)
            .Include(t => t.TutorBadges).ThenInclude(tb => tb.Badge)
            .Where(t => t.IsApproved)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(t =>
                (t.User != null && t.User.FullName != null && t.User.FullName.Contains(keyword)) ||
                (t.Bio != null && t.Bio.Contains(keyword)) ||
                (t.TeachingArea != null && t.TeachingArea.Contains(keyword)));

        if (subjectId.HasValue)
            query = query.Where(t => t.TutorSubjects.Any(ts => ts.SubjectId == subjectId.Value));

        // ✅ LỌC THEO CẤP HỌC: chỉ lấy gia sư có ít nhất 1 môn thuộc cấp đã chọn
        if (!string.IsNullOrWhiteSpace(level))
            query = query.Where(t =>
                t.TutorSubjects.Any(ts => ts.Subject != null && ts.Subject.Level == level));

        if (!string.IsNullOrWhiteSpace(mode))
            query = query.Where(t => t.TeachingMode == mode || t.TeachingMode == "Both");

        query = query.Where(t => t.HourlyRate >= minPrice && t.HourlyRate <= maxPrice);

        var list = await query.ToListAsync();

        var results = list.Select(t => new SearchResult
        {
            Profile = t,
            AvgRating = t.ReceivedReviews.Any() ? t.ReceivedReviews.Average(r => r.Rating) : 0,
            ReviewCount = t.ReceivedReviews.Count,
            CompletedCount = t.Bookings.Count(b => b.Status == "Completed")
        })
        .Where(r => r.AvgRating >= minRating)
        .ToList();

        results = sortBy switch
        {
            "price_asc" => results.OrderBy(r => r.Profile.HourlyRate).ToList(),
            "price_desc" => results.OrderByDescending(r => r.Profile.HourlyRate).ToList(),
            "newest" => results.OrderByDescending(r => r.Profile.Id).ToList(),
            _ => results.OrderByDescending(r => r.AvgRating).ThenByDescending(r => r.ReviewCount).ToList()
        };

        ViewBag.TotalCount = results.Count;
        ViewBag.TotalPages = (int)Math.Ceiling((double)results.Count / pageSize);
        ViewBag.Page = page;
        ViewBag.Subjects = await _db.Subjects.Where(s => s.IsActive).ToListAsync();

        // ✅ Danh sách cấp học (lấy động từ các môn đang hoạt động) để đổ vào dropdown
        ViewBag.Levels = await _db.Subjects
            .Where(s => s.IsActive && s.Level != null && s.Level != "")
            .Select(s => s.Level)
            .Distinct()
            .ToListAsync();

        ViewBag.Keyword = keyword;
        ViewBag.SubjectId = subjectId;
        ViewBag.Mode = mode;
        ViewBag.Level = level;          // ✅ giữ lại lựa chọn để hiển thị
        ViewBag.MinPrice = minPrice;
        ViewBag.MaxPrice = maxPrice;
        ViewBag.MinRating = minRating;
        ViewBag.SortBy = sortBy;

        return View(results.Skip((page - 1) * pageSize).Take(pageSize).ToList());
    }
}
