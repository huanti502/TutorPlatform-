using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.Services;

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

    // GET /Battle — Trang chọn đối thủ + môn
    public async Task<IActionResult> Index()
    {
        var me = await _userManager.GetUserAsync(User);
        var subjects = await _db.Subjects.Where(s => s.IsActive).ToListAsync();

        // Lấy danh sách người dùng có thể thách đấu (trừ mình)
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

    // GET /Battle/Room/{roomId} — Trang chiến đấu
    public async Task<IActionResult> Room(string roomId)
    {
        var me = await _userManager.GetUserAsync(User);
        ViewBag.RoomId = roomId;
        ViewBag.MyId = me!.Id;
        ViewBag.MyName = me.FullName;
        return View();
    }

    // POST /Battle/GenerateQuestions — AI tạo câu hỏi cho trận (gọi từ client)
    [HttpPost]
    public async Task<IActionResult> GenerateQuestions([FromBody] BattleQuizRequest req)
    {
        var subject = await _db.Subjects.FindAsync(req.SubjectId);
        if (subject == null) return Json(new { error = "Không tìm thấy môn" });

        var systemPrompt = """
            Bạn là giáo viên. Tạo bộ câu hỏi trắc nghiệm cho trận đấu.
            Chỉ trả về JSON thuần túy, KHÔNG markdown, KHÔNG ```json```.
            Cấu trúc:
            {"questions":[{"id":1,"question":"...","options":["A...","B...","C...","D..."],"correctIndex":0}]}
            Tạo đúng 10 câu, không có trường explanation.
            """;

        var userPrompt = $"Tạo 10 câu trắc nghiệm ngắn môn {subject.Name}, cấp độ {req.Level}. Câu hỏi súc tích để trả lời nhanh trong 60 giây.";

        try
        {
            var raw = await _ai.ChatAsync(systemPrompt, userPrompt, maxTokens: 2000);
            raw = raw.Trim();
            if (raw.StartsWith("```"))
                raw = string.Join("\n", raw.Split('\n').Skip(1).SkipLast(1));

            using var doc = System.Text.Json.JsonDocument.Parse(raw);
            return Json(new { success = true, data = doc.RootElement.Clone() });
        }
        catch
        {
            return Json(new { success = false, error = "AI đang bận, thử lại." });
        }
    }

    public class BattleQuizRequest
    {
        public int SubjectId { get; set; }
        public string Level { get; set; } = "Trung bình";
    }
}