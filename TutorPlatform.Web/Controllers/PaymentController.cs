using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.Services;

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
    public async Task<IActionResult> Checkout(int bookingId)
    {
        var booking = await _db.Bookings
            .Include(b => b.TutorProfile).ThenInclude(t => t.User)
            .Include(b => b.Subject)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null || booking.Status != "Confirmed" || booking.IsPaid)
            return RedirectToAction("MyBookings", "Booking");

        // Chỉ học viên sở hữu booking mới được thanh toán (chặn IDOR).
        var userId = _userManager.GetUserId(User);
        if (booking.StudentId != userId)
            return RedirectToAction("MyBookings", "Booking");

        // Tính tiền = số giờ * học phí/giờ của gia sư.
        double totalHours = (booking.EndTime - booking.StartTime).TotalHours;
        decimal totalPrice = Math.Round((decimal)totalHours * booking.TutorProfile.HourlyRate);
        if (totalPrice < 5000) totalPrice = 5000; // VNPay yêu cầu số tiền tối thiểu

        // Tạo bản ghi giao dịch (Pending).
        long orderCode = DateTime.Now.Ticks;
        var payment = new Payment
        {
            BookingId = booking.Id,
            OrderCode = orderCode,
            Amount = totalPrice,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
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
