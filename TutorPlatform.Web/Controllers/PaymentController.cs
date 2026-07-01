using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.Services;
using TutorPlatform.Web.ViewModels;

namespace TutorPlatform.Web.Controllers;

[Authorize(Roles = "Student")]
public class PaymentController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly IConfiguration _config;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(AppDbContext db, UserManager<AppUser> userManager,
        IConfiguration config, ILogger<PaymentController> logger)
    {
        _db = db;
        _userManager = userManager;
        _config = config;
        _logger = logger;
    }

    // Bắt đầu thanh toán: tạo giao dịch + chuyển hướng sang VNPay.
    // Trang xác nhận thanh toán (hiển thị tổng tiền + ô nhập mã giảm giá).
    [HttpGet]
    public async Task<IActionResult> Checkout(int bookingId)
    {
        var booking = await LoadOwnedBookingAsync(bookingId);
        if (booking == null) return RedirectToAction("MyBookings", "Booking");

        double hours = (booking.EndTime - booking.StartTime).TotalHours;
        var vm = new CheckoutViewModel
        {
            BookingId = booking.Id,
            TutorName = booking.TutorProfile.User?.FullName ?? "Gia sư",
            SubjectName = booking.Subject?.Name ?? "Buổi học",
            TimeText = booking.StartTime.AddHours(7).ToString("HH:mm dd/MM/yyyy"),
            Hours = Math.Round(hours, 1),
            HourlyRate = booking.TutorProfile.HourlyRate,
            BaseAmount = BaseAmount(booking)
        };
        return View(vm);
    }

    // AJAX: kiểm tra mã giảm giá, trả về số tiền sau giảm.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyCoupon(int bookingId, string code)
    {
        var booking = await LoadOwnedBookingAsync(bookingId);
        if (booking == null) return Json(new { ok = false, message = "Không tìm thấy buổi học." });

        var baseAmount = BaseAmount(booking);
        var (coupon, discount, error) = await ValidateCouponAsync(code, baseAmount);
        if (error != null) return Json(new { ok = false, message = error });
        if (coupon == null) return Json(new { ok = false, message = "Vui lòng nhập mã." });

        var final = Math.Max(baseAmount - discount, 5000);
        var realDiscount = baseAmount - final;

        return Json(new
        {
            ok = true,
            code = coupon.Code,
            discount = realDiscount,
            finalAmount = final,
            message = $"Áp dụng mã {coupon.Code} thành công!"
        });
    }

    // Thực hiện thanh toán: tạo giao dịch + chuyển sang VNPay.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int bookingId, string? couponCode)
    {
        var booking = await LoadOwnedBookingAsync(bookingId);
        if (booking == null) return RedirectToAction("MyBookings", "Booking");

        var baseAmount = BaseAmount(booking);

        // Kiểm tra lại mã ở server (không tin client).
        var (coupon, discount, error) = await ValidateCouponAsync(couponCode, baseAmount);
        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction("Checkout", new { bookingId });
        }

        var totalPrice = Math.Max(baseAmount - discount, 5000); // VNPay tối thiểu 5000
        var realDiscount = baseAmount - totalPrice;

        // Tạo bản ghi giao dịch (Pending).
        long orderCode = DateTime.Now.Ticks;
        var payment = new Payment
        {
            BookingId = booking.Id,
            OrderCode = orderCode,
            Amount = totalPrice,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            CouponCode = coupon?.Code,
            DiscountAmount = realDiscount
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        // Dựng URL thanh toán VNPay.
        var tmnCode = _config["Vnpay:TmnCode"];
        var hashSecret = _config["Vnpay:HashSecret"];
        var baseUrl = _config["Vnpay:BaseUrl"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
        var version = _config["Vnpay:Version"] ?? "2.1.0";

        if (string.IsNullOrWhiteSpace(tmnCode) || string.IsNullOrWhiteSpace(hashSecret))
        {
            _logger.LogWarning("Chưa cấu hình Vnpay:TmnCode / Vnpay:HashSecret.");
            TempData["Error"] = "Hệ thống thanh toán chưa được cấu hình.";
            return RedirectToAction("MyBookings", "Booking");
        }

        var createDate = DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss"); // giờ VN
        var ipAddr = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var returnUrl = $"{Request.Scheme}://{Request.Host}/Payment/VnPayReturn";

        var vnp = new VnPayLibrary();
        vnp.AddRequestData("vnp_Version", version);
        vnp.AddRequestData("vnp_Command", "pay");
        vnp.AddRequestData("vnp_TmnCode", tmnCode);
        vnp.AddRequestData("vnp_Amount", ((long)(totalPrice * 100)).ToString()); // x100 theo VNPay
        vnp.AddRequestData("vnp_CreateDate", createDate);
        vnp.AddRequestData("vnp_CurrCode", "VND");
        vnp.AddRequestData("vnp_IpAddr", ipAddr);
        vnp.AddRequestData("vnp_Locale", "vn");
        vnp.AddRequestData("vnp_OrderInfo", $"Thanh toan lich hoc {booking.Id}");
        vnp.AddRequestData("vnp_OrderType", "other");
        vnp.AddRequestData("vnp_ReturnUrl", returnUrl);
        vnp.AddRequestData("vnp_TxnRef", orderCode.ToString());

        var paymentUrl = vnp.CreateRequestUrl(baseUrl, hashSecret);
        return Redirect(paymentUrl);
    }

    // ===== Helpers =====

    // Lấy booking thuộc về học viên hiện tại và còn ở trạng thái thanh toán được.
    private async Task<Booking?> LoadOwnedBookingAsync(int bookingId)
    {
        var booking = await _db.Bookings
            .Include(b => b.TutorProfile).ThenInclude(t => t.User)
            .Include(b => b.Subject)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null || booking.Status != "Confirmed" || booking.IsPaid) return null;

        var userId = _userManager.GetUserId(User);
        if (booking.StudentId != userId) return null; // chặn IDOR

        return booking;
    }

    private static decimal BaseAmount(Booking b)
    {
        double hours = (b.EndTime - b.StartTime).TotalHours;
        decimal price = Math.Round((decimal)hours * b.TutorProfile.HourlyRate);
        return price < 5000 ? 5000 : price;
    }

    // Trả về (coupon, số tiền giảm, lỗi). coupon == null & error == null nghĩa là không nhập mã.
    private async Task<(Coupon? coupon, decimal discount, string? error)> ValidateCouponAsync(string? code, decimal baseAmount)
    {
        if (string.IsNullOrWhiteSpace(code)) return (null, 0, null);

        var norm = code.Trim().ToUpperInvariant();
        var c = await _db.Coupons.FirstOrDefaultAsync(x => x.Code == norm);

        if (c == null) return (null, 0, "Mã giảm giá không tồn tại.");
        if (!c.IsActive) return (null, 0, "Mã giảm giá đã bị vô hiệu hoá.");
        if (c.ExpiresAt.HasValue && c.ExpiresAt.Value < DateTime.UtcNow) return (null, 0, "Mã giảm giá đã hết hạn.");
        if (c.UsageLimit > 0 && c.UsedCount >= c.UsageLimit) return (null, 0, "Mã giảm giá đã hết lượt sử dụng.");
        if (baseAmount < c.MinOrder) return (null, 0, $"Đơn tối thiểu {c.MinOrder:N0}đ mới dùng được mã này.");

        decimal discount = c.DiscountType == "Percent"
            ? Math.Round(baseAmount * c.DiscountValue / 100m)
            : c.DiscountValue;

        if (c.DiscountType == "Percent" && c.MaxDiscount.HasValue && discount > c.MaxDiscount.Value)
            discount = c.MaxDiscount.Value;
        if (discount > baseAmount) discount = baseAmount;

        return (c, discount, null);
    }

    // Biên nhận thanh toán (in / lưu PDF từ trình duyệt).
    [HttpGet]
    public async Task<IActionResult> Receipt(int bookingId)
    {
        var payment = await _db.Payments
            .Include(p => p.Booking).ThenInclude(b => b!.Student)
            .Include(p => p.Booking).ThenInclude(b => b!.Subject)
            .Include(p => p.Booking).ThenInclude(b => b!.TutorProfile).ThenInclude(t => t.User)
            .Where(p => p.BookingId == bookingId && p.Status == "Paid")
            .OrderByDescending(p => p.PaidAt)
            .FirstOrDefaultAsync();

        if (payment == null)
        {
            TempData["Error"] = "Không tìm thấy biên nhận cho buổi học này.";
            return RedirectToAction("MyBookings", "Booking");
        }

        var uid = _userManager.GetUserId(User);
        var isOwner = payment.Booking!.StudentId == uid;
        var isTutor = payment.Booking.TutorProfile.UserId == uid;
        var isAdmin = User.IsInRole("Admin");
        if (!isOwner && !isTutor && !isAdmin) return Forbid();

        return View(payment);
    }

    // VNPay chuyển hướng về sau khi thanh toán.
    public async Task<IActionResult> VnPayReturn()
    {
        var vnp = new VnPayLibrary();
        foreach (var (key, value) in Request.Query)
        {
            if (key.StartsWith("vnp_"))
                vnp.AddResponseData(key, value.ToString());
        }

        var orderCodeStr = vnp.GetResponseData("vnp_TxnRef");
        var responseCode = vnp.GetResponseData("vnp_ResponseCode");
        var transactionNo = vnp.GetResponseData("vnp_TransactionNo");
        var secureHash = Request.Query["vnp_SecureHash"].ToString();
        var hashSecret = _config["Vnpay:HashSecret"] ?? "";

        // Bước bảo mật: kiểm tra chữ ký để chống giả mạo.
        if (!vnp.ValidateSignature(secureHash, hashSecret))
        {
            TempData["Error"] = "Chữ ký không hợp lệ — giao dịch không được xác thực.";
            return RedirectToAction("MyBookings", "Booking");
        }

        long.TryParse(orderCodeStr, out var orderCode);
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.OrderCode == orderCode);
        if (payment == null)
        {
            TempData["Error"] = "Không tìm thấy giao dịch.";
            return RedirectToAction("MyBookings", "Booking");
        }

        if (payment.Status == "Paid")
        {
            TempData["Success"] = "Giao dịch này đã được thanh toán.";
            return RedirectToAction("MyBookings", "Booking");
        }

        if (responseCode == "00")
        {
            payment.Status = "Paid";
            payment.TransactionNo = transactionNo;
            payment.ResponseCode = responseCode;
            payment.PaidAt = DateTime.UtcNow;

            var booking = await _db.Bookings.FindAsync(payment.BookingId);
            if (booking != null) booking.IsPaid = true;

            // Tăng lượt dùng của mã giảm giá (nếu có).
            if (!string.IsNullOrEmpty(payment.CouponCode))
            {
                var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Code == payment.CouponCode);
                if (coupon != null) coupon.UsedCount++;
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Thanh toán thành công! 🎉";
        }
        else
        {
            payment.Status = "Failed";
            payment.ResponseCode = responseCode;
            await _db.SaveChangesAsync();
            TempData["Error"] = $"Thanh toán không thành công (mã lỗi {responseCode}).";
        }

        return RedirectToAction("MyBookings", "Booking");
    }
}
