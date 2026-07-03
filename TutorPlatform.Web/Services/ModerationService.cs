using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Infrastructure.Data;

namespace TutorPlatform.Web.Services;

// #18: Kiểm duyệt nội dung — regex tức thời + danh sách từ cấm do ADMIN quản lý (DB) + lớp AI
public class ModerationService
{
    private readonly AIService? _ai;
    private readonly AppDbContext? _db;
    public ModerationService(AIService? ai = null, AppDbContext? db = null) { _ai = ai; _db = db; }

    private static readonly Regex PhoneRe = new(@"(\+?84|0)\s?\d{2,3}[\s.\-]?\d{3}[\s.\-]?\d{3,4}", RegexOptions.Compiled);
    private static readonly Regex BankRe = new(@"\b(stk|số tài khoản|so tai khoan|chuyển khoản|chuyen khoan|momo|zalopay|vietcombank|techcombank|mbbank|bidv|agribank)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex OffPlatformRe = new(@"(giao dịch ngoài|thanh toán ngoài|học ngoài (nền tảng|app|web)|liên hệ ngoài|bỏ qua trung gian)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // cache từ cấm 60 giây để không truy vấn DB mỗi tin nhắn
    private static List<string> _cache = new();
    private static DateTime _cacheAt = DateTime.MinValue;
    private static readonly object _lock = new();

    private List<string> Words()
    {
        if (_db == null) return _cache;
        if ((DateTime.UtcNow - _cacheAt).TotalSeconds < 60) return _cache;
        lock (_lock)
        {
            if ((DateTime.UtcNow - _cacheAt).TotalSeconds < 60) return _cache;
            try
            {
                _cache = _db.BannedWords.AsNoTracking().Select(w => w.Word.ToLowerInvariant()).ToList();
                _cacheAt = DateTime.UtcNow;
            }
            catch { }
            return _cache;
        }
    }

    public static void InvalidateCache() => _cacheAt = DateTime.MinValue;

    public (bool Ok, string? Reason) QuickCheck(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return (true, null);
        var t = text.ToLowerInvariant();
        if (PhoneRe.IsMatch(text)) return (false, "Nội dung chứa số điện thoại — vui lòng trao đổi qua nền tảng để được bảo vệ.");
        if (BankRe.IsMatch(t)) return (false, "Nội dung liên quan chuyển khoản/tài khoản ngân hàng không được phép.");
        if (OffPlatformRe.IsMatch(t)) return (false, "Nội dung gợi ý giao dịch ngoài nền tảng không được phép.");
        foreach (var w in Words())
            if (!string.IsNullOrWhiteSpace(w) && t.Contains(w)) return (false, "Nội dung chứa từ ngữ không phù hợp.");
        return (true, null);
    }

    public async Task<(bool Ok, string? Reason)> DeepCheckAsync(string? text)
    {
        var quick = QuickCheck(text);
        if (!quick.Ok) return quick;
        if (_ai == null || string.IsNullOrWhiteSpace(text) || text.Length < 15) return (true, null);
        try
        {
            var raw = await _ai.ChatAsync(
                "Bạn là bộ kiểm duyệt nội dung tiếng Việt cho nền tảng gia sư. Trả về DUY NHẤT một từ: SAFE nếu nội dung bình thường; UNSAFE nếu chứa xúc phạm, tục tĩu, lừa đảo, dụ dỗ giao dịch ngoài nền tảng, hoặc thông tin liên lạc cá nhân.",
                text, 10);
            return raw.Trim().ToUpperInvariant().Contains("UNSAFE")
                ? (false, "Nội dung không phù hợp theo kiểm duyệt AI.")
                : (true, null);
        }
        catch { return (true, null); }
    }
}
