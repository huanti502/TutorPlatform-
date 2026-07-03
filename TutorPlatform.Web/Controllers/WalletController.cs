using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.Services;

namespace TutorPlatform.Web.Controllers;

// #8: Ví & giữ tiền trung gian (escrow)
[Authorize]
public class WalletController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly NotificationService _notif;
    private readonly IConfiguration _config;

    public WalletController(AppDbContext db, UserManager<AppUser> userManager, NotificationService notif, IConfiguration config)
    {
        _db = db; _userManager = userManager; _notif = notif; _config = config;
    }

    public static async Task<decimal> BalanceAsync(AppDbContext db, string userId)
        => await db.WalletTransactions.Where(t => t.UserId == userId).SumAsync(t => (decimal?)t.Amount) ?? 0;

    public async Task<IActionResult> Index()
    {
        var uid = _userManager.GetUserId(User)!;
        ViewBag.Balance = await BalanceAsync(_db, uid);
        var txs = await _db.WalletTransactions
            .Where(t => t.UserId == uid)
            .OrderByDescending(t => t.CreatedAt)
            .Take(50).ToListAsync();
        return View(txs);
    }

    // Nạp ví (sandbox demo — thực tế sẽ đi qua cổng VNPay)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deposit(decimal amount)
    {
        var uid = _userManager.GetUserId(User)!;
        if (amount < 10000 || amount > 10000000)
        {
            TempData["Error"] = "Số tiền nạp phải từ 10.000đ đến 10.000.000đ.";
            return RedirectToAction("Index");
        }
        _db.WalletTransactions.Add(new WalletTransaction
        {
            UserId = uid, Amount = amount, Type = "Deposit",
            Description = "Nạp tiền vào ví (sandbox)"
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Đã nạp {amount:N0}đ vào ví.";
        return RedirectToAction("Index");
    }

    // Thanh toán buổi học bằng ví -> tiền bị GIỮ (escrow) tới khi buổi hoàn thành
    [Authorize(Roles = "Student")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PayBooking(int bookingId)
    {
        var uid = _userManager.GetUserId(User)!;
        var booking = await _db.Bookings
            .Include(b => b.TutorProfile).ThenInclude(t => t.User)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.StudentId == uid);
        if (booking == null) return NotFound();
        if (booking.IsPaid)
        {
            TempData["Error"] = "Buổi học này đã được thanh toán.";
            return RedirectToAction("MyBookings", "Booking");
        }

        var hours = (decimal)(booking.EndTime - booking.StartTime).TotalHours;
        var amount = Math.Round(hours * (booking.TutorProfile?.HourlyRate ?? 0), 0);
        var balance = await BalanceAsync(_db, uid);
        if (balance < amount)
        {
            TempData["Error"] = $"Số dư ví không đủ (cần {amount:N0}đ, hiện có {balance:N0}đ). Hãy nạp thêm.";
            return RedirectToAction("Index");
        }

        _db.WalletTransactions.Add(new WalletTransaction
        {
            UserId = uid, Amount = -amount, Type = "Hold", BookingId = booking.Id,
            Description = $"Giữ tiền buổi học #{booking.Id} (escrow) — sẽ chuyển cho gia sư khi hoàn thành"
        });
        booking.IsPaid = true;
        await _db.SaveChangesAsync();

        await _notif.NotifyAsync(booking.TutorProfile!.UserId, "Buổi học đã được thanh toán qua ví",
            "Tiền đang được nền tảng giữ và sẽ chuyển cho bạn khi buổi học hoàn thành.", "/Booking/TutorRequests");
        TempData["Success"] = $"Đã thanh toán {amount:N0}đ từ ví. Tiền được giữ an toàn tới khi buổi học hoàn thành.";
        return RedirectToAction("MyBookings", "Booking");
    }

    // === Các hàm tĩnh cho BookingController gọi khi Complete/Cancel ===
    public static async Task<bool> ReleaseToTutorAsync(AppDbContext db, IConfiguration config, Booking booking)
    {
        var hold = await db.WalletTransactions
            .FirstOrDefaultAsync(t => t.BookingId == booking.Id && t.Type == "Hold");
        if (hold == null) return false;
        var released = await db.WalletTransactions
            .AnyAsync(t => t.BookingId == booking.Id && (t.Type == "Release" || t.Type == "Refund"));
        if (released) return true;

        var gross = Math.Abs(hold.Amount);
        var rate = config.GetValue<decimal?>("Platform:CommissionRate") ?? 0.15m;
        var net = Math.Round(gross * (1 - rate), 0);
        db.WalletTransactions.Add(new WalletTransaction
        {
            UserId = booking.TutorProfile!.UserId, Amount = net, Type = "Release", BookingId = booking.Id,
            Description = $"Nhận tiền buổi học #{booking.Id} ({gross:N0}đ − {rate:P0} phí nền tảng)"
        });
        return true;
    }

    public static async Task<bool> RefundToStudentAsync(AppDbContext db, Booking booking, double refundRate)
    {
        var hold = await db.WalletTransactions
            .FirstOrDefaultAsync(t => t.BookingId == booking.Id && t.Type == "Hold");
        if (hold == null) return false;
        var done = await db.WalletTransactions
            .AnyAsync(t => t.BookingId == booking.Id && (t.Type == "Release" || t.Type == "Refund"));
        if (done) return true;

        var gross = Math.Abs(hold.Amount);
        var back = Math.Round(gross * (decimal)refundRate, 0);
        if (back > 0)
            db.WalletTransactions.Add(new WalletTransaction
            {
                UserId = booking.StudentId, Amount = back, Type = "Refund", BookingId = booking.Id,
                Description = $"Hoàn {(refundRate >= 1 ? "100%" : refundRate > 0 ? "50%" : "0%")} tiền buổi học #{booking.Id} về ví"
            });
        return true;
    }
}
