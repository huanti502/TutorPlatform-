using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;

namespace TutorPlatform.Web.Controllers;

// Hồ sơ công khai của học viên — để gia sư xem trước khi nhận lớp.
// Hiển thị cấp độ XP + đánh giá 2 chiều (gia sư đã đánh giá học viên này).
[Authorize]
public class StudentProfileController : Controller
{
    private readonly AppDbContext _db;

    public StudentProfileController(AppDbContext db) => _db = db;

    [HttpGet("/StudentProfile/{id}")]
    public async Task<IActionResult> Index(string id)
    {
        var student = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == "Student");
        if (student == null) return NotFound();

        var reviews = await _db.StudentReviews
            .Include(r => r.Booking).ThenInclude(b => b!.TutorProfile).ThenInclude(t => t!.User)
            .Where(r => r.StudentId == id)
            .OrderByDescending(r => r.CreatedAt)
            .Take(20)
            .ToListAsync();

        var completedCount = await _db.Bookings.CountAsync(b => b.StudentId == id && b.Status == "Completed");

        ViewBag.Student = student;
        ViewBag.CompletedCount = completedCount;
        ViewBag.AvgRating = reviews.Any() ? Math.Round(reviews.Average(r => r.Rating), 1) : 0.0;
        ViewBag.ReviewCount = reviews.Count;
        return View(reviews);
    }
}
