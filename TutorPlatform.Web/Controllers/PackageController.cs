using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.Services;

namespace TutorPlatform.Web.Controllers;

[Authorize]
public class PackageController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly IConfiguration _config;
    private readonly NotificationService _notif;

    public PackageController(AppDbContext db, UserManager<AppUser> userManager,
        IConfiguration config, NotificationService notif)
    {
        _db = db;
        _userManager = userManager;
        _config = config;
        _notif = notif;
    }

    // ===================== GIA SƯ =====================

    [Authorize(Roles = "Tutor")]
    [HttpGet]
    public async Task<IActionResult> Manage()
    {
        var uid = _userManager.GetUserId(User)!;
        var profile = await _db.TutorProfiles.FirstOrDefaultAsync(t => t.UserId == uid);
        if (profile == null) return RedirectToAction("Dashboard", "Tutor");

        var packages = await _db.LessonPackages
            .Where(p => p.TutorProfileId == profile.Id)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        ViewBag.HourlyRate = profile.HourlyRate;
        return View(packages);
    }

    [Authorize(Roles = "Tutor")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, int sessionCount, decimal price)
    {
        var uid = _userManager.GetUserId(User)!;
        var profile = await _db.TutorProfiles.FirstOrDefaultAsync(t => t.UserId == uid);
        if (profile == null) return RedirectToAction("Dashboard", "Tutor");

        if (string.IsNullOrWhiteSpace(name) || sessionCount < 1 || price < 1000)
        {
            TempData["Error"] = "Thông tin gói chưa hợp lệ.";
            return RedirectToAction("Manage");
        }

        _db.LessonPackages.Add(new LessonPackage
        {
            TutorProfileId = profile.Id,
            Name = name.Trim(),
            SessionCount = sessionCount,
            Price = price,
            IsActive = true
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã tạo gói học.";
        return RedirectToAction("Manage");
    }

    [Authorize(Roles = "Tutor")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var uid = _userManager.GetUserId(User)!;
        var pkg = await _db.LessonPackages.Include(p => p.TutorProfile).FirstOrDefaultAsync(p => p.Id == id);
        if (pkg != null && pkg.TutorProfile!.UserId == uid)
        {
            pkg.IsActive = !pkg.IsActive;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("Manage");
    }

    // ===================== HỌC VIÊN =====================

    [Authorize(Roles = "Student")]
    [HttpGet]
    public async Task<IActionResult> MyPackages()
    {
        var uid = _userManager.GetUserId(User)!;
        var purchases = await _db.PackagePurchases
            .Include(p => p.LessonPackage).ThenInclude(lp => lp!.TutorProfile).ThenInclude(t => t!.User)
            .Where(p => p.StudentId == uid && p.Status != "Pending")
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
        return View(purchases);
    }

    // Mua gói -> tạo lượt mua Pending + chuyển sang VNPay.
    [Authorize(Roles = "Student")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Buy(int packageId)
    {
        var uid = _userManager.GetUserId(User)!;
        var pkg = await _db.LessonPackages.FirstOrDefaultAsync(p => p.Id == packageId && p.IsActive);
        if (pkg == null)
        {
            TempData["Error"] = "Gói học không khả dụng.";
            return RedirectToAction("MyPackages");
        }

        long orderCode = DateTime.Now.Ticks;
        var purchase = new PackagePurchase
        {
            LessonPackageId = pkg.Id,
            StudentId = uid,
            TutorProfileId = pkg.TutorProfileId,
            TotalSessions = pkg.SessionCount,
            RemainingSessions = pkg.SessionCount,
            PricePaid = pkg.Price,
            OrderCode = orderCode,
            Status = "Pending"
        };
        _db.PackagePurchases.Add(purchase);
        await _db.SaveChangesAsync();

        var tmnCode = _config["Vnpay:TmnCode"];
        var hashSecret = _config["Vnpay:HashSecret"];
        var baseUrl = _config["Vnpay:BaseUrl"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
        var version = _config["Vnpay:Version"] ?? "2.1.0";
        if (string.IsNullOrWhiteSpace(tmnCode) || string.IsNullOrWhiteSpace(hashSecret))
        {
            TempData["Error"] = "Hệ thống thanh toán chưa được cấu hình.";
            return RedirectToAction("MyPackages");
        }

        var createDate = DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss");
        var ipAddr = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var returnUrl = $"{Request.Scheme}://{Request.Host}/Package/PaymentReturn";

        var vnp = new VnPayLibrary();
        vnp.AddRequestData("vnp_Version", version);
        vnp.AddRequestData("vnp_Command", "pay");
        vnp.AddRequestData("vnp_TmnCode", tmnCode);
        vnp.AddRequestData("vnp_Amount", ((long)(pkg.Price * 100)).ToString());
        vnp.AddRequestData("vnp_CreateDate", createDate);
        vnp.AddRequestData("vnp_CurrCode", "VND");
        vnp.AddRequestData("vnp_IpAddr", ipAddr);
        vnp.AddRequestData("vnp_Locale", "vn");
        vnp.AddRequestData("vnp_OrderInfo", $"Mua goi hoc {pkg.Id}");
        vnp.AddRequestData("vnp_OrderType", "other");
        vnp.AddRequestData("vnp_ReturnUrl", returnUrl);
        vnp.AddRequestData("vnp_TxnRef", orderCode.ToString());

        return Redirect(vnp.CreateRequestUrl(baseUrl, hashSecret));
    }

    // VNPay gọi về sau khi mua gói.
    [HttpGet]
    public async Task<IActionResult> PaymentReturn()
    {
        var vnp = new VnPayLibrary();
        foreach (var (key, value) in Request.Query)
            if (key.StartsWith("vnp_")) vnp.AddResponseData(key, value.ToString());

        var orderCodeStr = vnp.GetResponseData("vnp_TxnRef");
        var responseCode = vnp.GetResponseData("vnp_ResponseCode");
        var secureHash = Request.Query["vnp_SecureHash"].ToString();
        var hashSecret = _config["Vnpay:HashSecret"] ?? "";

        if (!vnp.ValidateSignature(secureHash, hashSecret))
        {
            TempData["Error"] = "Chữ ký không hợp lệ.";
            return RedirectToAction("MyPackages");
        }

        long.TryParse(orderCodeStr, out var orderCode);
        var purchase = await _db.PackagePurchases
            .Include(p => p.LessonPackage).ThenInclude(lp => lp!.TutorProfile)
            .FirstOrDefaultAsync(p => p.OrderCode == orderCode);
        if (purchase == null)
        {
            TempData["Error"] = "Không tìm thấy lượt mua.";
            return RedirectToAction("MyPackages");
        }
        if (purchase.Status == "Active")
        {
            TempData["Success"] = "Gói đã được kích hoạt.";
            return RedirectToAction("MyPackages");
        }

        if (responseCode == "00")
        {
            purchase.Status = "Active";
            purchase.ActivatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var tutorUserId = purchase.LessonPackage?.TutorProfile?.UserId;
            if (!string.IsNullOrEmpty(tutorUserId))
                await _notif.NotifyAsync(tutorUserId, "📦 Học viên mua gói",
                    $"Một học viên vừa mua gói {purchase.TotalSessions} buổi của bạn.", "/Package/Manage");

            TempData["Success"] = $"Mua gói thành công! Bạn có {purchase.RemainingSessions} buổi.";
        }
        else
        {
            TempData["Error"] = $"Thanh toán không thành công (mã {responseCode}).";
        }
        return RedirectToAction("MyPackages");
    }

    // Dùng gói để thanh toán cho 1 booking (trừ 1 buổi).
    [Authorize(Roles = "Student")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UseForBooking(int bookingId)
    {
        var uid = _userManager.GetUserId(User)!;
        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId && b.StudentId == uid);
        if (booking == null || booking.IsPaid || booking.Status != "Confirmed")
        {
            TempData["Error"] = "Buổi học không hợp lệ để dùng gói.";
            return RedirectToAction("MyBookings", "Booking");
        }

        var purchase = await _db.PackagePurchases
            .Where(p => p.StudentId == uid && p.TutorProfileId == booking.TutorProfileId
                        && p.Status == "Active" && p.RemainingSessions > 0)
            .OrderBy(p => p.ActivatedAt)
            .FirstOrDefaultAsync();

        if (purchase == null)
        {
            TempData["Error"] = "Bạn chưa có gói còn buổi với gia sư này.";
            return RedirectToAction("MyBookings", "Booking");
        }

        purchase.RemainingSessions--;
        if (purchase.RemainingSessions <= 0) purchase.Status = "Used";
        booking.IsPaid = true;
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Đã dùng 1 buổi trong gói. Còn lại {purchase.RemainingSessions} buổi.";
        return RedirectToAction("MyBookings", "Booking");
    }
}
