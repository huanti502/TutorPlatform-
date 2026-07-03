using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;

namespace TutorPlatform.Web.Controllers;

public class TutorSearchController : Controller
{
    private readonly AppDbContext _db;
    private readonly TutorPlatform.Web.Services.AIService _ai;
    public TutorSearchController(AppDbContext db, TutorPlatform.Web.Services.AIService ai) { _db = db; _ai = ai; }

    public class SearchResult
    {
        public TutorProfile Profile { get; set; } = null!;
        public double AvgRating { get; set; }
        public int ReviewCount { get; set; }
        public int CompletedCount { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        string? keyword, int? subjectId, string? mode, string? level,   //  thêm "level"
        decimal minPrice = 0, decimal maxPrice = 1_000_000,
        double minRating = 0, string sortBy = "rating", int page = 1)
    {
        const int pageSize = 9;

        var query = _db.TutorProfiles
            .AsNoTracking() //  TĂNG TỐC: Giải phóng RAM
            .AsSplitQuery() //  TĂNG TỐC: Chia nhỏ truy vấn chống nghẽn
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

        //  LỌC THEO CẤP HỌC: chỉ lấy gia sư có ít nhất 1 môn thuộc cấp đã chọn
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

        //  Danh sách cấp học (lấy động từ các môn đang hoạt động) để đổ vào dropdown
        ViewBag.Levels = await _db.Subjects
            .Where(s => s.IsActive && s.Level != null && s.Level != "")
            .Select(s => s.Level)
            .Distinct()
            .ToListAsync();

        ViewBag.Keyword = keyword;
        ViewBag.SubjectId = subjectId;
        ViewBag.Mode = mode;
        ViewBag.Level = level;          //  giữ lại lựa chọn để hiển thị
        ViewBag.MinPrice = minPrice;
        ViewBag.MaxPrice = maxPrice;
        ViewBag.MinRating = minRating;
        ViewBag.SortBy = sortBy;

        //  Danh sách gia sư mà học viên hiện tại đã lưu (để tô nút tim)
        var favoriteIds = new List<int>();
        if (User.Identity?.IsAuthenticated == true)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            favoriteIds = await _db.Favorites
                .Where(f => f.StudentId == uid)
                .Select(f => f.TutorProfileId)
                .ToListAsync();
        }
        ViewBag.FavoriteIds = favoriteIds;

        return View(results.Skip((page - 1) * pageSize).Take(pageSize).ToList());
    }

    // ==========================================
    // #6 SO SÁNH GIA SƯ
    // ==========================================
    [HttpGet]
    public async Task<IActionResult> Compare(string ids)
    {
        var idList = (ids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x.Trim(), out var v) ? v : 0)
            .Where(v => v > 0).Distinct().Take(3).ToList();
        if (idList.Count < 2)
        {
            TempData["Error"] = "Hãy chọn 2-3 gia sư để so sánh.";
            return RedirectToAction("Search");
        }
        var tutors = await _db.TutorProfiles
            .Include(t => t.User)
            .Include(t => t.TutorSubjects).ThenInclude(ts => ts.Subject)
            .Include(t => t.ReceivedReviews)
            .Include(t => t.Bookings)
            .Where(t => idList.Contains(t.Id) && t.IsApproved)
            .ToListAsync();
        var results = tutors.Select(t => new SearchResult
        {
            Profile = t,
            AvgRating = t.ReceivedReviews.Any() ? Math.Round(t.ReceivedReviews.Average(r => r.Rating), 1) : 0,
            ReviewCount = t.ReceivedReviews.Count,
            CompletedCount = t.Bookings.Count(b => b.Status == "Completed")
        }).OrderBy(r => idList.IndexOf(r.Profile.Id)).ToList();
        return View(results);
    }

    // ==========================================
    // #7 WIZARD "TÌM GIA SƯ CHO TÔI"
    // ==========================================
    [HttpGet]
    public async Task<IActionResult> Wizard()
    {
        ViewBag.Subjects = await _db.Subjects.Where(x => x.IsActive).ToListAsync();
        return View(new List<SearchResult>());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Wizard(int subjectId, string? goal, decimal budget, string? mode, string? area)
    {
        ViewBag.Subjects = await _db.Subjects.Where(x => x.IsActive).ToListAsync();

        var tutors = await _db.TutorProfiles
            .Include(t => t.User)
            .Include(t => t.TutorSubjects).ThenInclude(ts => ts.Subject)
            .Include(t => t.ReceivedReviews)
            .Include(t => t.Bookings)
            .Where(t => t.IsApproved)
            .ToListAsync();

        var scored = tutors.Select(t =>
        {
            double score = 0;
            var avg = t.ReceivedReviews.Any() ? t.ReceivedReviews.Average(r => r.Rating) : 0;
            if (t.TutorSubjects.Any(ts => ts.SubjectId == subjectId)) score += 50; else score -= 100;
            if (budget > 0) score += t.HourlyRate <= budget ? 20 : -25;
            score += avg * 8;
            if (t.FaceVerified) score += 8;
            if (!string.IsNullOrWhiteSpace(mode) && mode != "Any" &&
                (t.TeachingMode == mode || t.TeachingMode == "Both")) score += 10;
            if (!string.IsNullOrWhiteSpace(area) &&
                (t.TeachingArea ?? "").ToLower().Contains(area.Trim().ToLower())) score += 12;
            score += Math.Min(t.Bookings.Count(b => b.Status == "Completed"), 20) * 0.5;
            return (Tutor: t, Score: score, Avg: avg);
        })
        .Where(x => x.Score > 0)
        .OrderByDescending(x => x.Score)
        .Take(3)
        .ToList();

        var results = scored.Select(x => new SearchResult
        {
            Profile = x.Tutor,
            AvgRating = Math.Round(x.Avg, 1),
            ReviewCount = x.Tutor.ReceivedReviews.Count,
            CompletedCount = x.Tutor.Bookings.Count(b => b.Status == "Completed")
        }).ToList();

        // AI viết 1 câu lý do đề xuất cho mỗi gia sư (lỗi thì dùng lý do rule-based)
        var reasons = new Dictionary<int, string>();
        if (results.Count > 0)
        {
            try
            {
                var subjName = (await _db.Subjects.FindAsync(subjectId))?.Name ?? "";
                var listTxt = string.Join("\n", results.Select(r =>
                    $"{r.Profile.Id}|{r.Profile.User?.FullName}|{subjName}|{r.Profile.HourlyRate:N0}đ/h|rating {r.AvgRating}|{r.Profile.ExperienceYears} năm KN|{r.Profile.TeachingArea}"));
                var raw = await _ai.ChatAsync(
                    "Bạn là tư vấn viên giáo dục. Với mỗi dòng 'id|tên|môn|giá|rating|kinh nghiệm|khu vực', viết MỘT câu tiếng Việt (dưới 25 từ) lý do nên chọn gia sư này cho mục tiêu của học viên. CHỈ trả về JSON array thuần: [{\"id\":1,\"reason\":\"...\"}]",
                    $"Mục tiêu học viên: {goal}\nDanh sách:\n{listTxt}", 800);
                raw = raw.Replace("```json", "").Replace("```", "").Trim();
                int a = raw.IndexOf('['); int b = raw.LastIndexOf(']');
                if (a >= 0 && b > a)
                {
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<List<WizReason>>(raw.Substring(a, b - a + 1),
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (parsed != null)
                        foreach (var r in parsed) reasons[r.Id] = r.Reason ?? "";
                }
            }
            catch { }
            foreach (var r in results)
                if (!reasons.ContainsKey(r.Profile.Id) || string.IsNullOrWhiteSpace(reasons[r.Profile.Id]))
                    reasons[r.Profile.Id] = $"Phù hợp môn bạn chọn, rating {r.AvgRating}/5 với {r.CompletedCount} buổi đã dạy.";
        }
        ViewBag.Reasons = reasons;
        ViewBag.Searched = true;
        ViewBag.Goal = goal; ViewBag.SubjectId = subjectId; ViewBag.Budget = budget; ViewBag.Mode = mode; ViewBag.Area = area;
        return View(results);
    }

    public class WizReason { public int Id { get; set; } public string? Reason { get; set; } }

    // ==========================================
    // #19 TÌM KIẾM NGÔN NGỮ TỰ NHIÊN
    // ==========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ParseQuery([FromBody] NlReq req)
    {
        if (string.IsNullOrWhiteSpace(req?.Query))
            return Json(new { ok = false, message = "Hãy nhập câu tìm kiếm." });
        try
        {
            var subjects = await _db.Subjects.Where(x => x.IsActive).Select(x => new { x.Id, x.Name }).ToListAsync();
            var subjList = string.Join(", ", subjects.Select(x => $"{x.Id}={x.Name}"));
            var raw = await _ai.ChatAsync(
                "Bạn là bộ phân tích câu tìm kiếm gia sư tiếng Việt. CHỈ trả về JSON object thuần, không markdown, dạng: " +
                "{\"keyword\":\"\",\"subjectId\":null,\"mode\":null,\"minPrice\":0,\"maxPrice\":1000000,\"minRating\":0}. " +
                $"subjectId chọn từ danh sách: {subjList} (null nếu không rõ). mode: Online/Offline/null. " +
                "Giá tiền: 'dưới 200k' nghĩa là maxPrice=200000. keyword là tên người hoặc khu vực nếu có, ngược lại để rỗng.",
                req.Query, 400);
            raw = raw.Replace("```json", "").Replace("```", "").Trim();
            int a = raw.IndexOf('{'); int b = raw.LastIndexOf('}');
            if (a < 0 || b <= a) return Json(new { ok = false, message = "AI không hiểu câu này, thử diễn đạt khác." });
            var f = System.Text.Json.JsonSerializer.Deserialize<NlFilter>(raw.Substring(a, b - a + 1),
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (f == null) return Json(new { ok = false, message = "AI không hiểu câu này." });

            var qs = new List<string>();
            if (!string.IsNullOrWhiteSpace(f.Keyword)) qs.Add("keyword=" + Uri.EscapeDataString(f.Keyword));
            if (f.SubjectId is > 0) qs.Add("subjectId=" + f.SubjectId);
            if (!string.IsNullOrWhiteSpace(f.Mode)) qs.Add("mode=" + f.Mode);
            if (f.MinPrice > 0) qs.Add("minPrice=" + (long)f.MinPrice);
            if (f.MaxPrice > 0 && f.MaxPrice < 1_000_000) qs.Add("maxPrice=" + (long)f.MaxPrice);
            if (f.MinRating > 0) qs.Add("minRating=" + f.MinRating);
            return Json(new { ok = true, url = "/TutorSearch/Search" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "") });
        }
        catch { return Json(new { ok = false, message = "AI đang bận, thử lại sau." }); }
    }

    public class NlReq { public string? Query { get; set; } }
    public class NlFilter
    {
        public string? Keyword { get; set; }
        public int? SubjectId { get; set; }
        public string? Mode { get; set; }
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
        public double MinRating { get; set; }
    }
}
