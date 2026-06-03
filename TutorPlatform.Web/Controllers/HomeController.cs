using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TutorPlatform.Core.Models; // Thêm namespace này để gọi AppUser
using TutorPlatform.Web.Models;
using TutorPlatform.Web.Services;

namespace TutorPlatform.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly RecommendationService _recommendation;
        // Đã đổi từ IdentityUser sang AppUser ở đây
        private readonly UserManager<AppUser> _userManager;

        // Đã đổi từ IdentityUser sang AppUser ở tham số constructor
        public HomeController(RecommendationService recommendation, UserManager<AppUser> userManager)
        {
            _recommendation = recommendation;
            _userManager = userManager;

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

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}