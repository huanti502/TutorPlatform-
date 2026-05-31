using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.Services;

namespace TutorPlatform.Web.Controllers;

public class LeaderboardController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public LeaderboardController(AppDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var topStudents = await _db.Users
            .Where(u => u.Role == "Student" && u.XpPoints > 0)
            .OrderByDescending(u => u.XpPoints)
            .Take(20)
            .Select(u => new LeaderboardEntry
            {
                UserId = u.Id,
                FullName = u.FullName,
                Avatar = u.AvatarUrl,
                XpPoints = u.XpPoints,
                XpLevel = u.XpLevel
            })
            .ToListAsync();

        // Gắn rank + icon
        for (int i = 0; i < topStudents.Count; i++)
        {
            topStudents[i].Rank = i + 1;
            var (_, icon, _) = XpService.GetLevel(topStudents[i].XpPoints);
            topStudents[i].LevelIcon = icon;
        }

        // Lấy vị trí của user hiện tại
        var currentUser = await _userManager.GetUserAsync(User);
        int myRank = 0;
        if (currentUser != null)
        {
            var allRanked = await _db.Users
                .Where(u => u.Role == "Student")
                .OrderByDescending(u => u.XpPoints)
                .Select(u => u.Id)
                .ToListAsync();
            myRank = allRanked.IndexOf(currentUser.Id) + 1;
        }

        ViewBag.MyRank = myRank;
        ViewBag.CurrentUser = currentUser;
        return View(topStudents);
    }

    public class LeaderboardEntry
    {
        public int Rank { get; set; }
        public string UserId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string? Avatar { get; set; }
        public int XpPoints { get; set; }
        public string XpLevel { get; set; } = "";
        public string LevelIcon { get; set; } = "";
    }
}