using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;

namespace TutorPlatform.Web.Controllers;

[Authorize]
public class FavoriteController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public FavoriteController(AppDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // POST: lưu / bỏ lưu một gia sư (bấm nút tim)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int tutorProfileId, string? returnUrl = null)
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null) return Challenge();

        var existing = await _db.Favorites
            .FirstOrDefaultAsync(f => f.StudentId == userId && f.TutorProfileId == tutorProfileId);

        if (existing != null)
        {
            _db.Favorites.Remove(existing);            // đã lưu rồi → bỏ lưu
        }
        else
        {
            // chỉ lưu nếu gia sư có thật
            var exists = await _db.TutorProfiles.AnyAsync(t => t.Id == tutorProfileId);
            if (exists)
            {
                _db.Favorites.Add(new Favorite
                {
                    StudentId = userId,
                    TutorProfileId = tutorProfileId
                });
            }
        }

        await _db.SaveChangesAsync();

        // quay lại trang trước đó (trang tìm kiếm / chi tiết)
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("MyFavorites");
    }

    // GET: danh sách gia sư đã lưu
    [HttpGet]
    public async Task<IActionResult> MyFavorites()
    {
        var userId = _userManager.GetUserId(User);

        var favorites = await _db.Favorites
            .Where(f => f.StudentId == userId)
            .Include(f => f.TutorProfile).ThenInclude(t => t!.User)
            .Include(f => f.TutorProfile).ThenInclude(t => t!.ReceivedReviews)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        return View(favorites);
    }
}
