using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Infrastructure.Data;

namespace TutorPlatform.Web.Controllers;

[Authorize(Roles = "Student")]
public class PaymentController : Controller
{
    private readonly AppDbContext _db;

    public PaymentController(AppDbContext db)
    {
        _db = db;
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
        var booking = await _db.Bookings.FindAsync(bookingId);
        if (booking != null)
        {
            booking.IsPaid = true;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Đã xác nhận thanh toán thành công!";
        }
        return RedirectToAction("MyBookings", "Booking");
    }
}