using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.Services;

namespace TutorPlatform.Web.Controllers;

[Authorize] // Yêu cầu đăng nhập để tránh người lạ đốt quota Groq API
public class AIController : Controller
{
    private readonly AIService _ai;
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public AIController(AIService ai, AppDbContext db, UserManager<AppUser> userManager)
    {
        _ai = ai;
        _db = db;
        _userManager = userManager;
    }

    // ══════════════════════════════════════════════════════
    //  CHATBOT AI TƯ VẤN (GIỮ NGUYÊN CODE CŨ)
    // ══════════════════════════════════════════════════════

    public IActionResult Chat() => View();

    // Trợ lý giọng nói (Web Speech API + Groq)
    public IActionResult Voice() => View();

    // Proxy giọng đọc tiếng Việt: server tải audio từ Google Translate TTS
    // rồi trả về cùng domain -> không bị trình duyệt chặn (CORS/tracking prevention).
    private static readonly HttpClient _ttsClient = new HttpClient();

    [HttpGet]
    public async Task<IActionResult> Tts(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return BadRequest();
        if (text.Length > 200) text = text.Substring(0, 200);

        var url = "https://translate.google.com/translate_tts?ie=UTF-8&tl=vi&client=tw-ob&q="
                  + Uri.EscapeDataString(text);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            var res = await _ttsClient.SendAsync(req);
            if (!res.IsSuccessStatusCode) return StatusCode(502);
            var bytes = await res.Content.ReadAsByteArrayAsync();
            return File(bytes, "audio/mpeg");
        }
        catch
        {
            return StatusCode(502);
        }
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest("Tin nhắn trống");

        var tutors = await _db.TutorProfiles
            .Include(t => t.User)
            .Include(t => t.TutorSubjects).ThenInclude(ts => ts.Subject)
            .Include(t => t.ReceivedReviews)
            .Include(t => t.TutorBadges).ThenInclude(tb => tb.Badge)
            .Where(t => t.IsApproved)
            .ToListAsync();

        var tutorContext = string.Join("\n", tutors.Select(t =>
        {
            var avg = t.ReceivedReviews.Any()
                ? t.ReceivedReviews.Average(r => r.Rating).ToString("0.#")
                : "Chưa có";
            var subjects = string.Join(", ",
                t.TutorSubjects.Select(ts => ts.Subject?.Name));
            var badges = t.TutorBadges.Any()
                ? string.Join(", ", t.TutorBadges.Select(tb => tb.Badge?.Name))
                : "Chưa có";

            return $"""
                - ID: {t.Id} | Tên: {t.User?.FullName}
                  Môn: {subjects} | Giá: {t.HourlyRate:N0} VNĐ/giờ
                  Khu vực: {t.TeachingArea} | Hình thức: {t.TeachingMode}
                  Kinh nghiệm: {t.ExperienceYears} năm | Rating: {avg}/5
                  Huy hiệu: {badges} | Bio: {t.Bio?.Substring(0, Math.Min(100, t.Bio?.Length ?? 0))}...
                """;
        }));

        var systemPrompt = $"""
            Bạn là trợ lý AI thông minh của nền tảng TutorPlatform - hệ thống kết nối gia sư và học viên.
            Nhiệm vụ của bạn là TƯ VẤN và GIỚI THIỆU gia sư phù hợp nhất cho học viên.

            DANH SÁCH GIA SƯ HIỆN CÓ TRONG HỆ THỐNG:
            {tutorContext}

            HƯỚNG DẪN TRẢ LỜI:
            - Luôn trả lời bằng tiếng Việt, thân thiện và chuyên nghiệp
            - Khi học viên hỏi về gia sư, hãy phân tích yêu cầu (môn học, ngân sách, khu vực, hình thức)
              rồi giới thiệu 2-3 gia sư phù hợp nhất với lý do cụ thể
            - Định dạng khi giới thiệu gia sư:
               **[Tên gia sư]** — [Giá]/giờ
               Môn: [môn] |  Khu vực: [khu vực]
               Rating: [số]/5 |  [số] năm kinh nghiệm
               Lý do phù hợp: [giải thích ngắn gọn]
               [Link xem hồ sơ: /Tutor/Detail/ID]
            - Nếu không có gia sư phù hợp, hãy gợi ý học viên mở rộng tiêu chí
            - Có thể trả lời câu hỏi về cách sử dụng nền tảng, cách đặt lịch, thanh toán
            - KHÔNG bịa ra thông tin gia sư không có trong danh sách
            - Giữ câu trả lời ngắn gọn, dễ đọc, dùng emoji hợp lý
            """;

        try
        {
            var history = request.History ?? new List<MessageItem>();
            var messages = history.Select(h => (h.Role, h.Content)).ToList();
            messages.Add(("user", request.Message));

            var reply = await _ai.MultiTurnChatAsync(systemPrompt, messages, maxTokens: 1000);
            return Json(new { success = true, reply });
        }
        catch
        {
            return Json(new { success = false, reply = "Xin lỗi, AI đang bận. Vui lòng thử lại sau! " });
        }
    }

    // ══════════════════════════════════════════════════════
    //  AI TẠO LỘ TRÌNH HỌC CÁ NHÂN (TÍNH NĂNG MỚI)
    // ══════════════════════════════════════════════════════

    public async Task<IActionResult> StudyPlan()
    {
        var subjects = await _db.Subjects
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();
        return View(subjects);
    }

    [HttpPost]
    public async Task<IActionResult> GenerateStudyPlan([FromBody] StudyPlanRequest req)
    {
        var subject = await _db.Subjects.FindAsync(req.SubjectId);
        if (subject == null) return Json(new { error = "Không tìm thấy môn học" });

        // Lấy gia sư phù hợp từ DB
        var tutors = await _db.TutorProfiles
            .Include(t => t.User)
            .Include(t => t.TutorSubjects)
            .Include(t => t.ReceivedReviews)
            .Where(t => t.IsApproved &&
                        t.TutorSubjects.Any(ts => ts.SubjectId == req.SubjectId))
            .OrderByDescending(t => t.ReceivedReviews.Any()
                ? t.ReceivedReviews.Average(r => r.Rating) : 0)
            .Take(3)
            .ToListAsync();

        int daysLeft = Math.Max(7, (req.ExamDate - DateTime.Now).Days);
        int weeksLeft = Math.Max(2, daysLeft / 7);
        int hoursTotal = weeksLeft * req.HoursPerWeek;

        var weeks = BuildStudyPlan(
            subject.Name, req.CurrentLevel,
            req.Goal, req.WeakPoints,
            weeksLeft, req.HoursPerWeek);

        var recommendedTutors = tutors.Select(t => new
        {
            name = t.User.FullName,
            reason = $"Kinh nghiệm {t.ExperienceYears} năm, " +
                     $"học phí {t.HourlyRate:N0}đ/giờ, " +
                     $"hình thức {t.TeachingMode}"
        }).ToList();

        var materials = GetMaterials(subject.Name);
        var warnings = GetWarnings(req.CurrentLevel, daysLeft);

        string difficulty = req.CurrentLevel is "Mất gốc"or "Cơ bản"
            ? "Trung bình"
            : req.CurrentLevel == "Nâng cao" ? "Khó" : "Trung bình";

        var plan = new
        {
            overview = $"Lộ trình {weeksLeft} tuần giúp bạn đạt mục tiêu \"{req.Goal}\" " +
                       $"cho môn {subject.Name}. Tổng {hoursTotal} giờ học, " +
                       $"trung bình {req.HoursPerWeek} giờ/tuần.",
            totalWeeks = weeksLeft,
            hoursTotal = hoursTotal,
            difficulty = difficulty,
            weeks = weeks,
            recommendedTutors = recommendedTutors,
            materials = materials,
            warnings = warnings
        };

        var json = System.Text.Json.JsonSerializer.Serialize(plan);
        return Json(new { success = true, plan = json });
    }

    // ── Helpers tạo lộ trình ──────────────────────────────

    private List<object> BuildStudyPlan(string subject, string level,
        string goal, string weakPoints, int weeks, int hoursPerWeek)
    {
        var result = new List<object>();
        var phaseNames = GetPhaseNames(subject, weeks);

        for (int i = 0; i < weeks; i++)
        {
            double progress = (double)i / weeks;
            string theme, tips;
            List<string> goals, topics;

            if (progress < 0.25)
            {
                theme = phaseNames[0];
                goals = new() { "Nắm vững kiến thức nền tảng", "Xây dựng thói quen học tập" };
                topics = GetTopics(subject, "basic", i);
                tips = $"Tuần đầu quan trọng nhất — học chậm, hiểu kỹ. " +
                         $"Mỗi ngày dành {hoursPerWeek * 60 / 7} phút ôn lại.";
            }
            else if (progress < 0.6)
            {
                theme = phaseNames[1];
                goals = new() { "Luyện tập dạng bài trung cấp", "Cải thiện tốc độ làm bài" };
                topics = GetTopics(subject, "intermediate", i);
                tips = $"Giai đoạn này cần làm nhiều bài tập. " +
                         $"Chú ý đặc biệt: {(string.IsNullOrEmpty(weakPoints) ? "điểm yếu của bạn" : weakPoints)}.";
            }
            else if (progress < 0.85)
            {
                theme = phaseNames[2];
                goals = new() { "Nắm vững dạng bài nâng cao", $"Tiến gần mục tiêu: {goal}" };
                topics = GetTopics(subject, "advanced", i);
                tips = "Luyện đề thi thật theo thời gian quy định. Ghi lại lỗi sai để tránh.";
            }
            else
            {
                theme = phaseNames[3];
                goals = new() { "Ôn tập tổng hợp", "Tự tin vào kỳ thi" };
                topics = new() { "Ôn tập toàn bộ chương trình", "Làm đề thi thử", "Review điểm yếu" };
                tips = "Không học kiến thức mới. Tập trung ôn lại, giữ sức khỏe và tinh thần tốt.";
            }

            result.Add(new
            {
                weekNumber = i + 1,
                theme = theme,
                goals = goals,
                topics = topics,
                hours = hoursPerWeek,
                tips = tips
            });
        }
        return result;
    }

    private List<string> GetPhaseNames(string subject, int weeks)
    {
        return subject switch
        {
            "Toán học"or "Toán cao cấp" => new()
            {
                "Đại số & Giải tích cơ bản",
                "Luyện tập dạng bài trung cấp",
                "Hình học & Bài toán nâng cao",
                "Sprint cuối — Ôn thi tổng lực"
            },
            "Tiếng Anh"or "IELTS" => new()
            {
                "Vocabulary & Grammar Foundation",
                "4 Skills: Reading & Listening",
                "4 Skills: Writing & Speaking",
                "Mock Tests & Final Review"
            },
            "Vật lý" => new()
            {
                "Cơ học & Động lực học",
                "Điện từ & Quang học",
                "Luyện đề & Dạng bài tổng hợp",
                "Sprint cuối — Ôn thi tổng lực"
            },
            "Hóa học" => new()
            {
                "Hóa vô cơ cơ bản",
                "Hóa hữu cơ & Phản ứng",
                "Bài tập nâng cao & Nhận biết",
                "Sprint cuối — Ôn thi tổng lực"
            },
            "Lập trình" => new()
            {
                "Tư duy lập trình & Cú pháp cơ bản",
                "Cấu trúc dữ liệu & Hàm",
                "Project thực tế & Debug",
                "Hoàn thiện portfolio & Review"
            },
            "Tiếng Nhật" => new()
            {
                "Hiragana, Katakana & Từ vựng N5",
                "Ngữ pháp N5-N4 cơ bản",
                "Luyện nghe, đọc & Hội thoại",
                "Mock JLPT & Ôn tổng lực"
            },
            "Ngữ văn" => new()
            {
                "Đọc hiểu & Phân tích văn bản",
                "Nghị luận xã hội",
                "Nghị luận văn học chuyên sâu",
                "Luyện đề & Hoàn thiện kỹ năng"
            },
            "Lịch sử" => new()
            {
                "Lịch sử Việt Nam giai đoạn 1",
                "Lịch sử Việt Nam giai đoạn 2",
                "Lịch sử Thế giới & Tổng hợp",
                "Luyện đề thi & Ôn tổng lực"
            },
            _ => new()
            {
                "Kiến thức nền tảng",
                "Luyện tập chuyên sâu",
                "Nâng cao & Tổng hợp",
                "Ôn tập & Sprint cuối"
            }
        };
    }

    private List<string> GetTopics(string subject, string level, int weekIndex)
    {
        // Map chủ đề theo môn + level
        var map = new Dictionary<string, Dictionary<string, List<List<string>>>>
        {
            ["Toán học"] = new()
            {
                ["basic"] = new()
                {
                    new() { "Hàm số & Đồ thị", "Phương trình bậc 1, 2", "Bất phương trình" },
                    new() { "Đạo hàm cơ bản", "Ứng dụng đạo hàm", "Hàm số lượng giác" }
                },
                ["intermediate"] = new()
                {
                    new() { "Tích phân bất định", "Tích phân xác định", "Ứng dụng tích phân" },
                    new() { "Số phức", "Tổ hợp & Xác suất", "Dãy số & Cấp số" }
                },
                ["advanced"] = new()
                {
                    new() { "Hình học không gian", "Tọa độ trong không gian", "Khối đa diện" },
                    new() { "Đề thi thử THPTQG", "Dạng bài khó & mẹo giải nhanh", "Review sai lầm" }
                }
            },
            ["Tiếng Anh"] = new()
            {
                ["basic"] = new()
                {
                    new() { "Từ vựng chủ đề Education", "Thì hiện tại & quá khứ", "Reading: Skimming" },
                    new() { "Từ vựng chủ đề Technology", "Thì tương lai & hoàn thành", "Listening: Gap-fill" }
                },
                ["intermediate"] = new()
                {
                    new() { "Writing Task 1: Biểu đồ", "Speaking Part 1 & 2", "Reading: True/False" },
                    new() { "Writing Task 2: Opinion essay", "Speaking Part 3", "Listening: Multiple choice" }
                },
                ["advanced"] = new()
                {
                    new() { "Academic Vocabulary nâng cao", "Complex grammar", "Full Reading practice" },
                    new() { "Mock IELTS Test 1", "Mock IELTS Test 2", "Phân tích điểm yếu" }
                }
            },
            ["Vật lý"] = new()
            {
                ["basic"] = new()
                {
                    new() { "Động học chất điểm", "Động lực học", "Các định luật Newton" },
                    new() { "Công & Năng lượng", "Động lượng & Va chạm", "Chuyển động tròn" }
                },
                ["intermediate"] = new()
                {
                    new() { "Điện trường & Tụ điện", "Dòng điện không đổi", "Từ trường" },
                    new() { "Cảm ứng điện từ", "Dao động điều hòa", "Sóng cơ học" }
                },
                ["advanced"] = new()
                {
                    new() { "Quang học sóng", "Vật lý hạt nhân", "Lượng tử ánh sáng" },
                    new() { "Đề thi thử tổng hợp", "Bài tập khó đặc trưng", "Review toàn bộ" }
                }
            },
            ["Lập trình"] = new()
            {
                ["basic"] = new()
                {
                    new() { "Biến, kiểu dữ liệu, toán tử", "Câu lệnh điều kiện if/else", "Vòng lặp for/while" },
                    new() { "Hàm & tham số", "Mảng & danh sách", "String & xử lý chuỗi" }
                },
                ["intermediate"] = new()
                {
                    new() { "OOP: Class & Object", "OOP: Kế thừa & Đa hình", "Exception Handling" },
                    new() { "Collections & LINQ", "File I/O", "Debugging & Testing" }
                },
                ["advanced"] = new()
                {
                    new() { "Design Patterns cơ bản", "API RESTful", "Database & ORM" },
                    new() { "Project thực tế", "Deploy ứng dụng", "Code Review & Refactor" }
                }
            }
        };

        if (map.TryGetValue(subject, out var subMap) &&
            subMap.TryGetValue(level, out var levelList) &&
            levelList.Count > 0)
            return levelList[weekIndex % levelList.Count];

        // Default fallback
        return level switch
        {
            "basic" => new() { "Lý thuyết cơ bản", "Bài tập ví dụ", "Bài tập tự luyện" },
            "intermediate" => new() { "Dạng bài trung cấp", "Kỹ thuật giải nhanh", "Đề thi thử" },
            "advanced" => new() { "Chuyên đề nâng cao", "Đề thi thật", "Phân tích & Review" },
            _ => new() { "Ôn tập tổng hợp", "Đề thi mock", "Củng cố kiến thức" }
        };
    }

    private List<string> GetMaterials(string subject)
    {
        return subject switch
        {
            "Toán học"or "Toán cao cấp" => new()
            {
                "Sách giáo khoa + SBT Toán 12",
                "Đề thi THPTQG các năm (2018-2024)",
                "App: Photomath, Wolfram Alpha (miễn phí)",
                "YouTube: Thầy Nguyễn Thanh Tùng"
            },
            "Tiếng Anh" => new()
            {
                "Cambridge English Grammar in Use",
                "App: Duolingo, Anki (miễn phí)",
                "BBC Learning English (website miễn phí)",
                "IELTS Cambridge 14-17"
            },
            "IELTS" => new()
            {
                "Cambridge IELTS 14, 15, 16, 17",
                "IELTS Liz — website luyện thi miễn phí",
                "App: IELTS.org Official, Magoosh",
                "British Council free resources"
            },
            "Vật lý" => new()
            {
                "SGK Vật lý 12 + SBT nâng cao",
                "Đề thi THPTQG Vật lý các năm",
                "App: PhET Simulations (miễn phí)",
                "YouTube: Thầy Phạm Quốc Toản"
            },
            "Hóa học" => new()
            {
                "SGK Hóa học 12 + SBT",
                "Bảng tuần hoàn tương tác — ptable.com",
                "App: Chemistry by Design",
                "Đề thi HSG Hóa các tỉnh"
            },
            "Lập trình" => new()
            {
                "freeCodeCamp.org (hoàn toàn miễn phí)",
                "CS50 Harvard — khóa học online free",
                "GitHub Student Pack (tài khoản sinh viên)",
                "YouTube: Traversy Media, Fireship"
            },
            "Tiếng Nhật" => new()
            {
                "Genki I & II — giáo trình chuẩn",
                "App: Anki + deck JLPT N5-N2",
                "WaniKani — học Kanji hiệu quả",
                "NHK Web Easy — đọc báo tiếng Nhật"
            },
            "Ngữ văn" => new()
            {
                "SGK Ngữ văn 12 + Sách bài tập",
                "Tuyển tập đề thi THPTQG Văn",
                "Sách: Phân tích tác phẩm văn học 12",
                "YouTube: các kênh dạy văn THPT"
            },
            "Lịch sử" => new()
            {
                "SGK Lịch sử 12 + Atlat",
                "Đề thi THPTQG Lịch sử các năm",
                "Sách: Ôn tập Lịch sử theo chủ đề",
                "App: Quizlet — học thuộc mốc lịch sử"
            },
            _ => new()
            {
                "Sách giáo khoa & sách bài tập",
                "Đề thi các năm trước",
                "YouTube — tìm kiếm theo tên môn",
                "App học tập phù hợp với môn"
            }
        };
    }

    private List<string> GetWarnings(string level, int daysLeft)
    {
        var warnings = new List<string>();

        if (daysLeft < 30)
            warnings.Add("Thời gian rất gấp — ưu tiên ôn phần trọng tâm, bỏ qua chi tiết nhỏ");
        if (daysLeft < 14)
            warnings.Add("Còn dưới 2 tuần — KHÔNG học kiến thức mới, chỉ ôn và làm đề");
        if (level is "Mất gốc"or "Cơ bản")
            warnings.Add("Đừng vội học nâng cao khi chưa vững cơ bản — nền tảng quan trọng hơn");

        warnings.Add("Nghỉ ngơi đủ giấc (7-8 tiếng/đêm) — não cần ngủ để ghi nhớ kiến thức");
        warnings.Add("Học đều đặn mỗi ngày hiệu quả hơn nhồi nhét cuối tuần rất nhiều");
        warnings.Add("Tìm gia sư để được hướng dẫn trực tiếp, tránh học sai hướng mất thời gian");

        return warnings;
    }

    // ══════════════════════════════════════════════════════
    //  MODELS
    // ══════════════════════════════════════════════════════

    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public List<MessageItem>? History { get; set; }
    }

    public class MessageItem
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class StudyPlanRequest
    {
        public int SubjectId { get; set; }
        public string CurrentLevel { get; set; } = "";
        public string Goal { get; set; } = "";
        public DateTime ExamDate { get; set; }
        public int HoursPerWeek { get; set; }
        public string WeakPoints { get; set; } = "";
    }
}