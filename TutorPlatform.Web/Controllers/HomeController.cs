using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TutorPlatform.Core.Models; // Thêm namespace này để gọi AppUser
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.Models;
using TutorPlatform.Web.Services;
using TutorPlatform.Web.ViewModels;

namespace TutorPlatform.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly RecommendationService _recommendation;
        // Đã đổi từ IdentityUser sang AppUser ở đây
        private readonly UserManager<AppUser> _userManager;
        private readonly AppDbContext _db;

        // Đã đổi từ IdentityUser sang AppUser ở tham số constructor
        public HomeController(RecommendationService recommendation, UserManager<AppUser> userManager, AppDbContext db)
        {
            _recommendation = recommendation;
            _userManager = userManager;
            _db = db;

        }

        // Sửa thành async Task<IActionResult>
        public async Task<IActionResult> Index()
        {
            // ── Gia sư gợi ý (AI) ────────────────────────────────
            if (User.Identity?.IsAuthenticated == true && User.IsInRole("Student"))
            {
                var userId = _userManager.GetUserId(User);
                try
                {
                    var recommendations = await _recommendation.GetRecommendationsAsync(userId!, topN: 6);
                    ViewBag.Recommendations = recommendations;
                    ViewBag.HasRecommendations = recommendations.Any();
                }
                catch
                {
                    ViewBag.HasRecommendations = false;
                }
            }

            // ── Gia sư nổi bật / Gia sư của tháng ────────────────
            try
            {
                var tutors = await _db.TutorProfiles
                    .Where(t => t.IsApproved)
                    .Include(t => t.User)
                    .Include(t => t.TutorSubjects).ThenInclude(ts => ts.Subject)
                    .Include(t => t.ReceivedReviews)
                    .ToListAsync();

                var completedCounts = await _db.Bookings
                    .Where(b => b.Status == "Completed")
                    .GroupBy(b => b.TutorProfileId)
                    .Select(g => new { Id = g.Key, Count = g.Count() })
                    .ToListAsync();
                var completedMap = completedCounts.ToDictionary(x => x.Id, x => x.Count);

                var featured = tutors.Select(t =>
                {
                    int reviewCount = t.ReceivedReviews.Count;
                    double avg = reviewCount > 0 ? t.ReceivedReviews.Average(r => r.Rating) : 0;
                    int completed = completedMap.TryGetValue(t.Id, out var c) ? c : 0;
                    return new FeaturedTutorVM
                    {
                        TutorProfileId = t.Id,
                        FullName = t.User?.FullName ?? "Gia sư",
                        AvatarUrl = t.User?.AvatarUrl,
                        Education = t.Education,
                        HourlyRate = t.HourlyRate,
                        AvgRating = Math.Round(avg, 1),
                        ReviewCount = reviewCount,
                        CompletedSessions = completed,
                        Subjects = t.TutorSubjects.Select(ts => ts.Subject.Name).Take(3).ToList(),
                        Score = avg * 20 + reviewCount * 5 + completed
                    };
                })
                .OrderByDescending(f => f.Score)
                .ThenByDescending(f => f.AvgRating)
                .Take(4)
                .ToList();

                if (featured.Any() && featured[0].ReviewCount > 0)
                    featured[0].IsTutorOfMonth = true;

                ViewBag.FeaturedTutors = featured;
            }
            catch
            {
                ViewBag.FeaturedTutors = new List<FeaturedTutorVM>();
            }

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(int? code)
        {
            ViewBag.Code = code;
            if (code.HasValue) Response.StatusCode = code.Value;
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}