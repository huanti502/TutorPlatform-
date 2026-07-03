using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.Services; // Thêm thư viện chứa XpService
using System;
using System.Linq;
using System.Threading.Tasks;

namespace TutorPlatform.Web.Controllers;

// Đã bỏ [Authorize(Roles = "Student")] ở class level để chia quyền chi tiết cho từng Action
public class ReviewController : Controller
{
    private readonly AppDbContext _db;
    private readonly TutorPlatform.Web.Services.ModerationService _moderation;
    private readonly UserManager<AppUser> _userManager;
    private readonly XpService _xpService; //  Khai báo XpService
    private readonly NotificationService _notif;

    public ReviewController(
        AppDbContext db,
        UserManager<AppUser> userManager,
        XpService xpService, //  Inject XpService
        NotificationService notif, TutorPlatform.Web.Services.ModerationService moderation)
    {
        _moderation = moderation;
        _db = db;
        _userManager = userManager;
        _xpService = xpService; //  Gán XpService
        _notif = notif;
    }

    // ==========================================
    // ACTIONS DÀNH CHO HỌC VIÊN (STUDENT)
    // ==========================================

    [Authorize(Roles = "Student")]
    [HttpGet]
    public async Task<IActionResult> Create(int bookingId)
    {
        var booking = await _db.Bookings
            .Include(b => b.TutorProfile).ThenInclude(t => t.User)
            .Include(b => b.Subject)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null || booking.Status != "Completed")
        {
            TempData["Error"] = "Chỉ có thể đánh giá buổi học đã hoàn thành.";
            return RedirectToAction("MyBookings", "Booking");
        }

        var alreadyReviewed = await _db.Reviews.AnyAsync(r => r.BookingId == bookingId);
        if (alreadyReviewed)
        {
            TempData["Error"] = "Bạn đã đánh giá buổi học này rồi.";
            return RedirectToAction("MyBookings", "Booking");
        }

        ViewBag.Booking = booking;
        return View();
    }

    [Authorize(Roles = "Student")]
    [HttpPost]
    public async Task<IActionResult> Create(int bookingId, int rating, string? comment)
    {
        var user = await _userManager.GetUserAsync(User);
        var booking = await _db.Bookings.FindAsync(bookingId);
        if (booking == null) return NotFound();

        //  Chỉ học viên sở hữu booking mới được đánh giá (chặn IDOR)
        if (booking.StudentId != user!.Id) return Forbid();

        //  Chỉ đánh giá buổi đã hoàn thành (POST có thể bị gọi trực tiếp, không qua GET)
        if (booking.Status != "Completed")
        {
            TempData["Error"] = "Chỉ có thể đánh giá buổi học đã hoàn thành.";
            return RedirectToAction("MyBookings", "Booking");
        }

        //  Chặn đánh giá trùng (1 booking chỉ 1 review)
        if (await _db.Reviews.AnyAsync(r => r.BookingId == bookingId))
        {
            TempData["Error"] = "Bạn đã đánh giá buổi học này rồi.";
            return RedirectToAction("MyBookings", "Booking");
        }

        //  Giới hạn số sao hợp lệ trong khoảng 1–5
        rating = Math.Clamp(rating, 1, 5);

        // #18: AI kiểm duyệt bình luận đánh giá
        var modCheck = await _moderation.DeepCheckAsync(comment);
        if (!modCheck.Ok)
        {
            TempData["Error"] = modCheck.Reason;
            return RedirectToAction("Create", new { bookingId });
        }

        _db.Reviews.Add(new Review
        {
            StudentId = user!.Id,
            TutorProfileId = booking.TutorProfileId,
            BookingId = bookingId,
            Rating = rating,
            Comment = comment
        });

        await _db.SaveChangesAsync();

        //  Thêm XP sau khi viết review thành công
        await _xpService.AwardXpAsync(user!.Id, "review_written");

        //  Báo cho gia sư biết có đánh giá mới
        var tp = await _db.TutorProfiles.FindAsync(booking.TutorProfileId);
        if (tp != null)
            await _notif.NotifyAsync(tp.UserId, "Đánh giá mới",
                $"{user.FullName} đã đánh giá {rating} buổi học của bạn.",
                "/Review/TutorReviews/" + tp.Id);

        TempData["Success"] = "Cảm ơn bạn đã đánh giá!";
        return RedirectToAction("MyBookings", "Booking");
    }

    // ==========================================
    // ACTIONS DÀNH CHO GIA SƯ (TUTOR)
    // ==========================================

    /// <summary>
    /// Gia sư phản hồi 1 đánh giá
    /// POST /Review/Reply
    /// </summary>
    [Authorize(Roles = "Tutor")]
    [HttpPost]
    public async Task<IActionResult> Reply(int reviewId, string content, int tutorProfileId)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            TempData["Error"] = "Nội dung phản hồi không được trống.";
            return RedirectToAction("TutorReviews", new { id = tutorProfileId });
        }

        // Kiểm tra review có thuộc về gia sư này không
        var review = await _db.Reviews
            .Include(r => r.TutorProfile)
            .FirstOrDefaultAsync(r => r.Id == reviewId);

        var user = await _userManager.GetUserAsync(User);
        var profile = await _db.TutorProfiles.FirstOrDefaultAsync(t => t.UserId == user!.Id);

        if (review == null || review.TutorProfileId != profile?.Id)
            return Forbid();

        // Chỉ reply 1 lần (hoặc cập nhật nếu đã có)
        var existing = await _db.ReviewReplies.FirstOrDefaultAsync(r => r.ReviewId == reviewId);
        if (existing != null)
        {
            existing.Content = content; // Cho phép sửa reply
            existing.CreatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.ReviewReplies.Add(new ReviewReply
            {
                ReviewId = reviewId,
                AuthorId = user!.Id,
                Content = content,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã gửi phản hồi!";
        // Quay lại trang Detail của gia sư sau khi gửi
        return RedirectToAction("Detail", "Tutor", new { id = profile?.Id });
    }

    // ==========================================
    // ACTIONS CÔNG KHAI (MỌI NGƯỜI ĐỀU XEM ĐƯỢC)
    // ==========================================

    /// <summary>
    /// Trang xem tất cả review của gia sư kèm bộ lọc sao
    /// GET /Review/TutorReviews?id=1&star=5
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> TutorReviews(int id, int star = 0)
    {
        var profile = await _db.TutorProfiles
            .Include(t => t.User)
            .Include(t => t.ReceivedReviews)
                .ThenInclude(r => r.Student)
            .Include(t => t.ReceivedReviews)
                .ThenInclude(r => r.Reply)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (profile == null) return NotFound();

        var reviews = profile.ReceivedReviews.AsQueryable();

        if (star > 0)
            reviews = reviews.Where(r => (int)r.Rating == star);

        // Thống kê phân phối sao
        var starDist = Enumerable.Range(1, 5).ToDictionary(
            s => s,
            s => profile.ReceivedReviews.Count(r => (int)r.Rating == s)
        );

        ViewBag.Profile = profile;
        ViewBag.StarDist = starDist;
        ViewBag.FilterStar = star;
        ViewBag.AvgRating = profile.ReceivedReviews.Any()
            ? profile.ReceivedReviews.Average(r => r.Rating) : 0;

        return View(reviews.OrderByDescending(r => r.CreatedAt).ToList());
    }
}