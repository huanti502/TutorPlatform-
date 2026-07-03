using System.Text.RegularExpressions;

namespace TutorPlatform.Web.Services;

// #18: Kiểm duyệt nội dung — lớp regex tức thời + lớp AI (cho nội dung ít tần suất)
public class ModerationService
{
    private readonly AIService _ai;
    public ModerationService(AIService ai) => _ai = ai;

    private static readonly Regex PhoneRe = new(@"(\+?84|0)\s?\d{2,3}[\s.\-]?\d{3}[\s.\-]?\d{3,4}", RegexOptions.Compiled);
    private static readonly Regex BankRe = new(@"\b(stk|số tài khoản|so tai khoan|chuyển khoản|chuyen khoan|momo|zalopay|vietcombank|techcombank|mbbank|bidv|agribank)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex OffPlatformRe = new(@"(giao dịch ngoài|thanh toán ngoài|học ngoài (nền tảng|app|web)|liên hệ ngoài|bỏ qua trung gian)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly string[] BadWords = { "đm", "đcm", "vcl", "vl", "cc", "lồn", "cặc", "địt", "đĩ", "óc chó", "ngu như", "mất dạy" };

    // Lớp 1: regex — nhanh, chạy cho mọi tin nhắn/nội dung
    public (bool Ok, string? Reason) QuickCheck(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return (true, null);
        var t = text.ToLowerInvariant();
        if (PhoneRe.IsMatch(text)) return (false, "Nội dung chứa số điện thoại — vui lòng trao đổi qua nền tảng để được bảo vệ.");
        if (BankRe.IsMatch(t)) return (false, "Nội dung liên quan chuyển khoản/tài khoản ngân hàng không được phép.");
        if (OffPlatformRe.IsMatch(t)) return (false, "Nội dung gợi ý giao dịch ngoài nền tảng không được phép.");
        foreach (var w in BadWords)
            if (t.Contains(w)) return (false, "Nội dung chứa từ ngữ không phù hợp.");
        return (true, null);
    }

    // Lớp 2: AI — cho nội dung ít tần suất (đánh giá, bài viết)
    public async Task<(bool Ok, string? Reason)> DeepCheckAsync(string? text)
    {
        var quick = QuickCheck(text);
        if (!quick.Ok) return quick;
        if (string.IsNullOrWhiteSpace(text) || text.Length < 15) return (true, null);
        try
        {
            var raw = await _ai.ChatAsync(
                "Bạn là bộ kiểm duyệt nội dung tiếng Việt cho nền tảng gia sư. Trả về DUY NHẤT một từ: SAFE nếu nội dung bình thường; UNSAFE nếu chứa xúc phạm, tục tĩu, lừa đảo, dụ dỗ giao dịch ngoài nền tảng, hoặc thông tin liên lạc cá nhân.",
                text, 10);
            return raw.Trim().ToUpperInvariant().Contains("UNSAFE")
                ? (false, "Nội dung không phù hợp theo kiểm duyệt AI.")
                : (true, null);
        }
        catch { return (true, null); } // AI bận -> không chặn oan
    }
}
