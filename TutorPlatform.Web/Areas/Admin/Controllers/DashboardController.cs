using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;

namespace TutorPlatform.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class DashboardController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public DashboardController(AppDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.TotalTutors = await _db.TutorProfiles.CountAsync();
        ViewBag.TotalStudents = await _db.Users.CountAsync(u => u.Role == "Student");
        ViewBag.TotalBookings = await _db.Bookings.CountAsync();
        ViewBag.PendingBookings = await _db.Bookings.CountAsync(b => b.Status == "Pending");
        ViewBag.RecentBookings = await _db.Bookings
            .Include(b => b.Student)
            .Include(b => b.TutorProfile).ThenInclude(t => t.User)
            .Include(b => b.Subject)
            .OrderByDescending(b => b.CreatedAt)
            .Take(10)
            .ToListAsync();
        // Thêm vào action Index() hiện có, sau các ViewBag cũ:
        ViewBag.CompletedBookings = await _db.Bookings.CountAsync(b => b.Status == "Completed");
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        ViewBag.BookingsThisMonth = await _db.Bookings.CountAsync(b => b.CreatedAt >= monthStart);
        var completed = await _db.Bookings.Include(b => b.TutorProfile)
    .Where(b => b.Status == "Completed").ToListAsync();
        ViewBag.TotalRevenue = completed
            .Where(b => b.TutorProfile != null)
            .Sum(b => (decimal)(b.EndTime - b.StartTime).TotalHours * b.TutorProfile!.HourlyRate);
        ViewBag.TopTutors = await _db.TutorProfiles
            .Include(t => t.User).Include(t => t.Bookings).Include(t => t.ReceivedReviews)
            .Where(t => t.IsApproved)
            .OrderByDescending(t => t.Bookings.Count(b => b.Status == "Completed"))
            .Take(5).ToListAsync();
        return View();

    }

    public async Task<IActionResult> Users()
    {
        var users = await _db.Users.ToListAsync();
        return View(users);
    }

    [ValidateAntiForgeryToken]
    [HttpPost]
    public async Task<IActionResult> ToggleLock(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user != null)
        {
            user.IsLocked = !user.IsLocked;
            await _userManager.UpdateAsync(user);
        }
        return RedirectToAction("Users");
    }

    public async Task<IActionResult> ApproveTutors()
    {
        var tutors = await _db.TutorProfiles
            .Include(t => t.User)
            .Include(t => t.TutorSubjects).ThenInclude(ts => ts.Subject)
            .ToListAsync();
        return View(tutors);
    }

    [ValidateAntiForgeryToken]
    [HttpPost]
    public async Task<IActionResult> ToggleApprove(int id)
    {
        var tutor = await _db.TutorProfiles.FindAsync(id);
        if (tutor != null)
        {
            tutor.IsApproved = !tutor.IsApproved;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("ApproveTutors");
    }

    public async Task<IActionResult> Subjects()
    {
        var subjects = await _db.Subjects.ToListAsync();
        return View(subjects);
    }

    [ValidateAntiForgeryToken]
    [HttpPost]
    public async Task<IActionResult> AddSubject(string name, string? description, string level)
    {
        if (string.IsNullOrWhiteSpace(name))
            return RedirectToAction("Subjects");

        _db.Subjects.Add(new Subject
        {
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Level = level,
            IsActive = true
        });
        await _db.SaveChangesAsync();
        return RedirectToAction("Subjects");
    }

    [ValidateAntiForgeryToken]
    [HttpPost]
    public async Task<IActionResult> ToggleSubject(int id)
    {
        var subject = await _db.Subjects.FindAsync(id);
        if (subject != null)
        {
            subject.IsActive = !subject.IsActive;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("Subjects");
    }

    // Thêm vào cuối class DashboardController:

    [HttpGet]
    public async Task<IActionResult> GetRevenueChart()
    {
        var data = new List<object>();
        for (int i = 11; i >= 0; i--)
        {
            var month = DateTime.UtcNow.AddMonths(-i);
            var start = new DateTime(month.Year, month.Month, 1);
            var end = start.AddMonths(1);

            var bookings = await _db.Bookings
                .Include(b => b.TutorProfile)
                .Where(b => b.Status == "Completed" && b.CreatedAt >= start && b.CreatedAt < end)
                .ToListAsync();

            var revenue = bookings.Sum(b =>
                (decimal)(b.EndTime - b.StartTime).TotalHours * b.TutorProfile.HourlyRate);

            data.Add(new
            {
                label = month.ToString("MM/yyyy"),
                revenue = Math.Round(revenue / 1000),  // nghìn VNĐ
                count = bookings.Count
            });
        }
        return Json(data);
    }

    [HttpGet]
    public async Task<IActionResult> GetGrowthChart()
    {
        var data = new List<object>();
        for (int i = 11; i >= 0; i--)
        {
            var month = DateTime.UtcNow.AddMonths(-i);
            var start = new DateTime(month.Year, month.Month, 1);
            var end = start.AddMonths(1);

            data.Add(new
            {
                label = month.ToString("MM/yyyy"),
                tutors = await _db.Users.CountAsync(u => u.Role == "Tutor" && u.CreatedAt >= start && u.CreatedAt < end),
                students = await _db.Users.CountAsync(u => u.Role == "Student" && u.CreatedAt >= start && u.CreatedAt < end)
            });
        }
        return Json(data);
    }
}