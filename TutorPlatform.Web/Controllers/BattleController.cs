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

            // 🚀 FIX 1: Thuật toán quét lấy cục JSON chuẩn xác 100% (bỏ qua mọi chữ rác xung quanh)
            int start = raw.IndexOf('{');
            int end = raw.LastIndexOf('}');
            if (start >= 0 && end >= start)
            {
                raw = raw.Substring(start, end - start + 1);
            }

            // 🚀 FIX 2: Ép dữ liệu vào Class C# thay vì dùng JsonElement ảo (để tránh lỗi Server 500)
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var data = JsonSerializer.Deserialize<AiQuizResponse>(raw, options);

            if (data == null || data.Questions == null || !data.Questions.Any())
            {
                return Json(new { success = false, error = "AI tạo câu hỏi thất bại, hãy thử lại." });
            }

            // 🚀 FIX 3: Tạo mảng an toàn với chuẩn chữ thường (camelCase) để gửi xuống cho Javascript
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
            Console.WriteLine("AI Error: " + ex.Message);
            return Json(new { success = false, error = "AI đang bận hoặc quá tải. Hãy tải lại trang (F5)." });
        }
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