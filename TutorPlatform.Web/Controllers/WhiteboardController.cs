using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;

namespace TutorPlatform.Web.Controllers;

[Authorize]
public class WhiteboardController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public WhiteboardController(AppDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(int bookingId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var booking = await _db.Bookings
            .Include(b => b.TutorProfile).ThenInclude(t => t.User)
            .Include(b => b.Student)
            .Include(b => b.Subject)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null) return NotFound();

        // Chỉ gia sư hoặc học sinh của booking đó mới vào được
        bool isStudent = booking.StudentId == user.Id;
        bool isTutor = booking.TutorProfile.UserId == user.Id;
        if (!isStudent && !isTutor) return Forbid();

        ViewBag.RoomId = $"wb-{bookingId}";
        ViewBag.UserName = user.FullName;
        ViewBag.IsStudent = isStudent;
        ViewBag.Subject = booking.Subject?.Name ?? "Bảng trắng";
        ViewBag.Partner = isStudent
            ? booking.TutorProfile.User.FullName
            : booking.Student.FullName;

        return View(booking);
    }
}