using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.Services;
using System.Text.Json;
using System.Linq;

namespace TutorPlatform.Web.Controllers;

[Authorize]
public class BattleController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly AIService _ai;

    public BattleController(AppDbContext db, UserManager<AppUser> userManager, AIService ai)
    {
        _db = db;
        _userManager = userManager;
        _ai = ai;
    }

    public async Task<IActionResult> Index()
    {
        var me = await _userManager.GetUserAsync(User);
        var subjects = await _db.Subjects.Where(s => s.IsActive).ToListAsync();

        var users = await _db.Users
            .Where(u => u.Id != me!.Id && (u.Role == "Student" || u.Role == "Tutor"))
            .OrderByDescending(u => u.XpPoints)
            .Take(20)
            .ToListAsync();

        ViewBag.Subjects = subjects;
        ViewBag.Users = users;
        ViewBag.Me = me;
        return View();
    }

    // Route mặc định là {controller}/{action}/{id?}, nên đoạn cuối URL
    // /Battle/Room/abc123 được bind vào "id". Nhận cả "id" (route) lẫn "roomId"
    // (query) để chắc chắn luôn lấy được mã phòng dù gọi kiểu nào.
    public async Task<IActionResult> Room(string? id, string? roomId)
    {
        var me = await _userManager.GetUserAsync(User);

        // Ưu tiên id từ route, fallback sang roomId từ query string.
        var actualRoomId = !string.IsNullOrWhiteSpace(id) ? id : roomId;

        ViewBag.RoomId = actualRoomId;
        ViewBag.MyId = me!.Id;
        ViewBag.MyName = me.FullName;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> GenerateQuestions([FromBody] BattleQuizRequest req)
    {
        var subject = await _db.Subjects.FindAsync(req.SubjectId);
        if (subject == null) return Json(new { success = false, error = "Không tìm thấy môn" });

        var systemPrompt = """
            Bạn là giáo viên. Tạo bộ câu hỏi trắc nghiệm cho trận đấu.
            Chỉ trả về JSON thuần túy, KHÔNG markdown, KHÔNG có lời chào hỏi.
            Cấu trúc bắt buộc:
            {"questions":[{"id":1,"question":"...","options":["A","B","C","D"],"correctIndex":0}]}
            Tạo đúng 10 câu, không có trường explanation.
            """;

        var userPrompt = $"Tạo 10 câu trắc nghiệm ngắn môn {subject.Name}, cấp độ {req.Level}. Câu hỏi súc tích để trả lời nhanh trong 60 giây.";

        try
        {
            var raw = await _ai.ChatAsync(systemPrompt, userPrompt, maxTokens: 2000);

            // Quét lấy cục JSON chuẩn (bỏ qua mọi chữ rác xung quanh)
            int start = raw.IndexOf('{');
            int end = raw.LastIndexOf('}');
            if (start >= 0 && end >= start)
            {
                raw = raw.Substring(start, end - start + 1);
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var data = JsonSerializer.Deserialize<AiQuizResponse>(raw, options);

            if (data == null || data.Questions == null || !data.Questions.Any())
            {
                // AI trả dữ liệu rỗng → dùng bộ dự phòng thay vì để trận bị kẹt.
                return Json(new { success = true, data = new { questions = FallbackQuestions(subject.Name) } });
            }

            var safeQuestions = data.Questions.Select(q => new
            {
                id = q.Id,
                question = q.Question,
                options = q.Options,
                correctIndex = q.CorrectIndex
            }).ToList();

            return Json(new { success = true, data = new { questions = safeQuestions } });
        }
        catch (Exception ex)
        {
            //  AI lỗi/quá tải/đổi model → KHÔNG để trận chết.
            // Trả về bộ câu hỏi dự phòng để cả 2 người chơi vẫn vào trận được.
            Console.WriteLine("AI Error (dùng câu hỏi dự phòng): " + ex.Message);
            return Json(new { success = true, data = new { questions = FallbackQuestions(subject.Name) } });
        }
    }

    // =========================================
    // BỘ CÂU HỎI DỰ PHÒNG (khi AI lỗi/quá tải)
    // Định dạng khớp client: id, question, options[4], correctIndex
    // =========================================
    private static List<object> FallbackQuestions(string subjectName)
    {
        var bank = new (string q, string[] opts, int correct)[]
        {
            ("2 + 2 = ?", new[] { "3", "4", "5", "6" }, 1),
            ("Thủ đô của Việt Nam là?", new[] { "TP. Hồ Chí Minh", "Hà Nội", "Đà Nẵng", "Huế" }, 1),
            ("10 × 5 = ?", new[] { "40", "45", "50", "55" }, 2),
            ("Số nguyên tố nhỏ nhất là?", new[] { "0", "1", "2", "3" }, 2),
            ("Ở áp suất thường, nước sôi ở bao nhiêu °C?", new[] { "50", "90", "100", "120" }, 2),
            ("1 giờ bằng bao nhiêu phút?", new[] { "30", "45", "60", "90" }, 2),
            ("Hình vuông có mấy cạnh?", new[] { "3", "4", "5", "6" }, 1),
            ("7 − 3 = ?", new[] { "2", "3", "4", "5" }, 2),
            ("Mặt trời mọc ở hướng nào?", new[] { "Tây", "Đông", "Nam", "Bắc" }, 1),
            ("100 ÷ 4 = ?", new[] { "20", "25", "30", "40" }, 1),
        };

        return bank.Select((item, idx) => (object)new
        {
            id = idx + 1,
            question = item.q,
            options = item.opts,
            correctIndex = item.correct
        }).ToList();
    }

    // =========================================
    // MODELS PHỤ TRỢ
    // =========================================
    public class BattleQuizRequest
    {
        public int SubjectId { get; set; }
        public string Level { get; set; } = "Trung bình";
    }

    public class AiQuizResponse
    {
        public List<AiQuestion> Questions { get; set; } = new();
    }

    public class AiQuestion
    {
        public int Id { get; set; }
        public string Question { get; set; } = "";
        public List<string> Options { get; set; } = new();
        public int CorrectIndex { get; set; }
    }
}
