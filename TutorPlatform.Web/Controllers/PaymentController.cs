using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;

namespace TutorPlatform.Web.Controllers;

[Authorize(Roles = "Student")]
public class PaymentController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public PaymentController(AppDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // Hiển thị trang quét mã QR
    public async Task<IActionResult> Checkout(int bookingId)
    {
        var booking = await _db.Bookings
            .Include(b => b.TutorProfile).ThenInclude(t => t.User)
            .Include(b => b.Subject)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null || booking.Status != "Confirmed" || booking.IsPaid)
            return RedirectToAction("MyBookings", "Booking");

        // ✅ Chỉ học viên sở hữu booking mới được xem trang thanh toán (chặn IDOR)
        var userId = _userManager.GetUserId(User);
        if (booking.StudentId != userId)
            return RedirectToAction("MyBookings", "Booking");

        // 1. Tính toán số tiền (Số giờ * Học phí của gia sư)
        double totalHours = (booking.EndTime - booking.StartTime).TotalHours;
        decimal totalPrice = (decimal)totalHours * booking.TutorProfile.HourlyRate;

        // 2. Cấu hình thông tin tài khoản ngân hàng của bạn (Hoặc của Admin)
        string bankId = "MB"; // Mã ngân hàng (VD: MB, VCB, TCB, ACB...)
        string accountNo = "0901234567"; // THAY BẰNG SỐ TÀI KHOẢN THẬT CỦA BẠN ĐỂ TEST
        string accountName = "BUI TIEN DAT"; // Tên chủ thẻ (Viết hoa không dấu)
        string addInfo = $"Thanh toan lich hoc {booking.Id}"; // Nội dung CK

        // 3. Tạo link ảnh VietQR động
        string qrUrl = $"https://img.vietqr.io/image/{bankId}-{accountNo}-compact2.png?amount={(long)totalPrice}&addInfo={Uri.EscapeDataString(addInfo)}&accountName={Uri.EscapeDataString(accountName)}";

        ViewBag.QrUrl = qrUrl;
        ViewBag.TotalPrice = totalPrice;
        return View(booking);
    }

    // Xử lý xác nhận đã thanh toán (Dùng tạm nút xác nhận thủ công)
    [HttpPost]
    public async Task<IActionResult> ConfirmPayment(int bookingId)
    {
        var userId = _userManager.GetUserId(User);
        var booking = await _db.Bookings.FindAsync(bookingId);

        // ✅ Chỉ học viên sở hữu booking, đã xác nhận và chưa thanh toán (chặn IDOR)
        if (booking != null && booking.StudentId == userId &&
            booking.Status == "Confirmed" && !booking.IsPaid)
        {
            booking.IsPaid = true;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Đã xác nhận thanh toán thành công!";
        }
        else
        {
            TempData["Error"] = "Không thể xác nhận thanh toán cho lịch học này.";
        }
        return RedirectToAction("MyBookings", "Booking");
    }
}