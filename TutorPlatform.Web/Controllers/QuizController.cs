using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.Services;

namespace TutorPlatform.Web.Controllers;

[Authorize]
public class QuizController : Controller
{
    private readonly AIService _ai;
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly XpService _xpService; // ✅ Khai báo XpService

    public QuizController(
        AIService ai,
        AppDbContext db,
        UserManager<AppUser> userManager,
        XpService xpService) // ✅ Inject XpService
    {
        _ai = ai;
        _db = db;
        _userManager = userManager;
        _xpService = xpService; // ✅ Gán XpService
    }

    // GET /Quiz — Trang chọn môn và cấp độ
    public async Task<IActionResult> Index()
    {
        var subjects = await _db.Subjects.Where(s => s.IsActive).ToListAsync();
        return View(subjects);
    }

    // POST /Quiz/Generate — AI sinh câu hỏi (trả JSON)
    [HttpPost]
    public async Task<IActionResult> Generate([FromBody] GenerateQuizRequest req)
    {
        var subject = await _db.Subjects.FindAsync(req.SubjectId);
        if (subject == null) return Json(new { error = "Không tìm thấy môn học" });

        var systemPrompt = """
            Bạn là giáo viên chuyên nghiệp. Nhiệm vụ: tạo bộ câu hỏi trắc nghiệm.
            
            QUAN TRỌNG: Chỉ trả về JSON thuần túy, KHÔNG có markdown, KHÔNG có ```json```.
            
            Cấu trúc JSON bắt buộc:
            {
              "questions": [
                {
                  "id": 1,
                  "question": "Nội dung câu hỏi?",
                  "options": ["A. ...", "B. ...", "C. ...", "D. ..."],
                  "correctIndex": 0,
                  "explanation": "Giải thích tại sao đáp án đúng..."
                }
              ]
            }
            
            Quy tắc:
            - Tạo đúng 10 câu hỏi
            - correctIndex là vị trí 0-3 tương ứng A/B/C/D
            - Câu hỏi rõ ràng, không mơ hồ
            - Giải thích ngắn gọn 1-2 câu
            - Không lặp lại câu hỏi
            """;

        var userPrompt = $"Tạo 10 câu trắc nghiệm môn {subject.Name}, cấp độ: {req.Level}. " +
                         $"Chủ đề tập trung: {req.Topic ?? "tổng hợp"}.";

        try
        {
            var raw = await _ai.ChatAsync(systemPrompt, userPrompt, maxTokens: 2000);

            // Parse JSON từ AI
            raw = raw.Trim();
            if (raw.StartsWith("```")) // Phòng trường hợp AI vẫn trả markdown
            {
                raw = string.Join("\n", raw.Split('\n').Skip(1).SkipLast(1));
            }

            using var doc = JsonDocument.Parse(raw);
            var cloned = doc.RootElement.Clone(); // Clone trước khi doc bị dispose
            return Json(new { success = true, data = cloned });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = "AI đang bận, thử lại sau: " + ex.Message });
        }
    }

    // POST /Quiz/Submit — Lưu kết quả
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmitQuizRequest req)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var attempt = new QuizAttempt
        {
            UserId = user.Id,
            SubjectId = req.SubjectId,
            Level = req.Level,
            Score = req.Score,
            TotalQuestions = req.Total
        };

        _db.QuizAttempts.Add(attempt);
        await _db.SaveChangesAsync();

        // ✅ Tự động kiểm tra và cộng điểm XP dựa trên kết quả bài Quiz
        if (req.Score == req.Total)
        {
            await _xpService.AwardXpAsync(user.Id, "quiz_perfect");
        }
        else if ((double)req.Score / req.Total >= 0.7)
        {
            await _xpService.AwardXpAsync(user.Id, "quiz_pass");
        }

        // Tính xếp hạng người dùng theo môn
        var percent = (double)req.Score / req.Total * 100;
        string badge = percent switch
        {
            >= 90 => "🏆 Xuất sắc",
            >= 70 => "⭐ Khá giỏi",
            >= 50 => "📚 Trung bình",
            _ => "💪 Cần cố gắng thêm"
        };

        return Json(new { success = true, badge, percent = (int)percent });
    }

    // GET /Quiz/History — Lịch sử làm bài của học viên
    [Authorize]
    public async Task<IActionResult> History()
    {
        var user = await _userManager.GetUserAsync(User);
        var attempts = await _db.QuizAttempts
            .Include(q => q.Subject)
            .Where(q => q.UserId == user!.Id)
            .OrderByDescending(q => q.CreatedAt)
            .Take(20)
            .ToListAsync();
        return View(attempts);
    }

    // Models
    public class GenerateQuizRequest
    {
        public int SubjectId { get; set; }
        public string Level { get; set; } = "Trung bình";
        public string? Topic { get; set; }
    }

    public class SubmitQuizRequest
    {
        public int SubjectId { get; set; }
        public string Level { get; set; } = "";
        public int Score { get; set; }
        public int Total { get; set; }
    }
}
    // POST /Quiz/Submit — Lưu kết quả
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmitQuizRequest req)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        // ✅ Chặn dữ liệu bịa từ client: Total phải hợp lệ, Score phải trong [0..Total]
        if (req.Total <= 0)
            return Json(new { success = false, error = "Số câu hỏi không hợp lệ." });

        var safeScore = Math.Clamp(req.Score, 0, req.Total);

        var attempt = new QuizAttempt
        {
            UserId = user.Id,
            SubjectId = req.SubjectId,
            Level = req.Level,
            Score = safeScore,          // ✅ dùng điểm đã kẹp
            TotalQuestions = req.Total
        };

        _db.QuizAttempts.Add(attempt);
        await _db.SaveChangesAsync();

        // ✅ Cộng XP dựa trên điểm ĐÃ KẸP
        if (safeScore == req.Total)
        {
            await _xpService.AwardXpAsync(user.Id, "quiz_perfect");
        }
        else if ((double)safeScore / req.Total >= 0.7)
        {
            await _xpService.AwardXpAsync(user.Id, "quiz_pass");
        }

        var percent = (double)safeScore / req.Total * 100;
        string badge = percent switch
        {
            >= 90 => "🏆 Xuất sắc",
            >= 70 => "⭐ Khá giỏi",
            >= 50 => "📚 Trung bình",
            _ => "💪 Cần cố gắng thêm"
        };

        return Json(new { success = true, badge, percent = (int)percent });
    }