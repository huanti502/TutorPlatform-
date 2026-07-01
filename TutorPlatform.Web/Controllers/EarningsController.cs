using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;

namespace TutorPlatform.Web.Controllers;

[Authorize(Roles = "Tutor")]
public class EarningsController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly IConfiguration _config;

    public EarningsController(AppDbContext db, UserManager<AppUser> userManager, IConfiguration config)
    {
        _db = db;
        _userManager = userManager;
        _config = config;
    }

    public async Task<IActionResult> Index()
    {
        var uid = _userManager.GetUserId(User)!;
        var rate = _config.GetValue<decimal?>("Platform:CommissionRate") ?? 0.15m;

        var profile = await _db.TutorProfiles.FirstOrDefaultAsync(t => t.UserId == uid);
        var profileId = profile?.Id ?? -1;

        var payments = await _db.Payments
            .Include(p => p.Booking).ThenInclude(b => b!.Student)
            .Include(p => p.Booking).ThenInclude(b => b!.Subject)
            .Include(p => p.Booking).ThenInclude(b => b!.TutorProfile)
            .Where(p => p.Status == "Paid" && p.Booking!.TutorProfile.UserId == uid)
            .OrderByDescending(p => p.PaidAt)
            .ToListAsync();

        // Doanh thu từ gói đã kích hoạt
        var packageSales = await _db.PackagePurchases
            .Include(p => p.LessonPackage)
            .Where(p => p.TutorProfileId == profileId && p.Status != "Pending")
            .OrderByDescending(p => p.ActivatedAt)
            .ToListAsync();

        var grossFromPayments = payments.Sum(p => p.Amount);
        var grossFromPackages = packageSales.Sum(p => p.PricePaid);
        var gross = grossFromPayments + grossFromPackages;
        var commission = Math.Round(gross * rate);
        var net = gross - commission;

        // Biểu đồ 6 tháng gần nhất (thu nhập ròng theo tháng).
        var now = DateTime.UtcNow;
        var labels = new List<string>();
        var data = new List<decimal>();
        for (int i = 5; i >= 0; i--)
        {
            var month = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
            labels.Add(month.ToString("MM/yyyy"));
            var monthGross =
                payments.Where(p => p.PaidAt.HasValue && p.PaidAt.Value.Year == month.Year && p.PaidAt.Value.Month == month.Month).Sum(p => p.Amount)
                + packageSales.Where(p => p.ActivatedAt.HasValue && p.ActivatedAt.Value.Year == month.Year && p.ActivatedAt.Value.Month == month.Month).Sum(p => p.PricePaid);
            data.Add(monthGross - Math.Round(monthGross * rate));
        }

        ViewBag.Gross = gross;
        ViewBag.GrossFromPackages = grossFromPackages;
        ViewBag.Commission = commission;
        ViewBag.Net = net;
        ViewBag.Rate = rate;
        ViewBag.PaidCount = payments.Count + packageSales.Count;
        ViewBag.PackageSales = packageSales;
        ViewBag.ChartLabels = System.Text.Json.JsonSerializer.Serialize(labels);
        ViewBag.ChartData = System.Text.Json.JsonSerializer.Serialize(data);

        return View(payments);
    }
}
