using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.Services;

namespace TutorPlatform.Web.Controllers;

[Authorize]
public class ReferralController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly NotificationService _notif;

    // Cấu hình phần thưởng giới thiệu
    private const decimal RewardPercent = 15m;
    private const decimal RewardMaxDiscount = 100000m;

    public ReferralController(AppDbContext db, UserManager<AppUser> userManager, NotificationService notif)
    {
        _db = db;
        _userManager = userManager;
        _notif = notif;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        // Sinh mã giới thiệu nếu chưa có.
        if (string.IsNullOrEmpty(user.ReferralCode))
        {
            user.ReferralCode = await GenerateUniqueReferralCodeAsync();
            await _userManager.UpdateAsync(user);
        }

        ViewBag.MyCode = user.ReferralCode;
        ViewBag.HasBeenReferred = user.HasBeenReferred;
        ViewBag.ReferredCount = await _db.Referrals.CountAsync(r => r.ReferrerId == user.Id);
        ViewBag.ShareLink = $"{Request.Scheme}://{Request.Host}/?ref={user.ReferralCode}";
        ViewBag.RewardPercent = RewardPercent;

        // Các mã thưởng mình đã nhận (từ việc giới thiệu hoặc được giới thiệu).
        var myCodes = await _db.Referrals
            .Where(r => r.ReferrerId == user.Id || r.RefereeId == user.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => r.ReferrerId == user.Id ? r.ReferrerCouponCode : r.RefereeCouponCode)
            .ToListAsync();
        ViewBag.MyRewardCodes = myCodes;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(string code)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        if (user.HasBeenReferred)
        {
            TempData["Error"] = "Bạn đã nhập mã giới thiệu trước đó rồi.";
            return RedirectToAction("Index");
        }
        if (string.IsNullOrWhiteSpace(code))
        {
            TempData["Error"] = "Vui lòng nhập mã giới thiệu.";
            return RedirectToAction("Index");
        }

        var norm = code.Trim().ToUpperInvariant();
        if (norm == user.ReferralCode)
        {
            TempData["Error"] = "Không thể dùng mã của chính bạn.";
            return RedirectToAction("Index");
        }

        var referrer = await _db.Users.FirstOrDefaultAsync(u => u.ReferralCode == norm);
        if (referrer == null)
        {
            TempData["Error"] = "Mã giới thiệu không tồn tại.";
            return RedirectToAction("Index");
        }

        // Tạo 2 coupon riêng (dùng lại hệ thống mã giảm giá).
        var refereeCode = await CreateRewardCouponAsync();
        var referrerCode = await CreateRewardCouponAsync();

        _db.Referrals.Add(new Referral
        {
            ReferrerId = referrer.Id,
            RefereeId = user.Id,
            ReferrerCouponCode = referrerCode,
            RefereeCouponCode = refereeCode,
            CreatedAt = DateTime.UtcNow
        });
        user.HasBeenReferred = true;
        await _db.SaveChangesAsync();
        await _userManager.UpdateAsync(user);

        await _notif.NotifyAsync(referrer.Id, "🎁 Giới thiệu thành công",
            $"{user.FullName} đã dùng mã của bạn. Bạn nhận mã giảm giá: {referrerCode}.", "/Referral");
        await _notif.NotifyAsync(user.Id, "🎁 Nhận thưởng giới thiệu",
            $"Bạn nhận mã giảm giá: {refereeCode}.", "/Referral");

        TempData["Success"] = $"Thành công! Mã giảm giá của bạn: {refereeCode} (giảm {RewardPercent:0}%).";
        return RedirectToAction("Index");
    }

    // ===== Helpers =====

    private async Task<string> GenerateUniqueReferralCodeAsync()
    {
        string code;
        do { code = RandomCode(6); }
        while (await _db.Users.AnyAsync(u => u.ReferralCode == code));
        return code;
    }

    private async Task<string> CreateRewardCouponAsync()
    {
        string code;
        do { code = "REF" + RandomCode(5); }
        while (await _db.Coupons.AnyAsync(c => c.Code == code));

        _db.Coupons.Add(new Coupon
        {
            Code = code,
            DiscountType = "Percent",
            DiscountValue = RewardPercent,
            MaxDiscount = RewardMaxDiscount,
            MinOrder = 0,
            UsageLimit = 1,
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddDays(90),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return code;
    }

    private static readonly char[] Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();
    private static string RandomCode(int len)
    {
        var rng = Random.Shared;
        var chars = new char[len];
        for (int i = 0; i < len; i++) chars[i] = Alphabet[rng.Next(Alphabet.Length)];
        return new string(chars);
    }
}
