using Microsoft.AspNetCore.Identity;
using TutorPlatform.Core.Models;

namespace TutorPlatform.Web.Services;

public class XpService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<XpService> _logger;

    // Bảng XP theo hành động
    public static readonly Dictionary<string, int> XpTable = new()
    {
        ["booking_completed"] = 50,   // Hoàn thành 1 buổi học
        ["review_written"] = 20,   // Viết đánh giá gia sư
        ["quiz_pass"] = 30,   // Làm quiz đạt ≥ 70%
        ["quiz_perfect"] = 60,   // Làm quiz đạt 100%
        ["post_created"] = 10,   // Đăng bài blog
        ["first_booking"] = 100,  // Đặt lịch lần đầu (bonus)
    };

    // Bảng cấp độ XP
    public static readonly List<(int minXp, string level, string icon)> Levels = new()
    {
        (0,    "Mới bắt đầu",   "🌱"),
        (100,  "Học viên",      "📖"),
        (300,  "Chăm chỉ",      "⚡"),
        (600,  "Thành thạo",    "🎯"),
        (1000, "Xuất sắc",      "🌟"),
        (2000, "Chuyên gia",    "💎"),
        (5000, "Huyền thoại",   "👑"),
    };

    public XpService(UserManager<AppUser> userManager, ILogger<XpService> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<int> AwardXpAsync(string userId, string action)
    {
        if (!XpTable.TryGetValue(action, out int xp)) return 0;

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return 0;

        user.XpPoints += xp;
        user.XpLevel = GetLevel(user.XpPoints).level;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("User {Id} nhận {Xp} XP từ hành động {Action}", userId, xp, action);
        return xp;
    }

    public static (string level, string icon, int nextLevelXp) GetLevel(int xp)
    {
        var current = Levels.Last(l => xp >= l.minXp);
        var currentIdx = Levels.IndexOf(current);
        int nextXp = currentIdx < Levels.Count - 1 ? Levels[currentIdx + 1].minXp : int.MaxValue;
        return (current.level, current.icon, nextXp);
    }
}