using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.Services;

namespace TutorPlatform.Web.Controllers;

[Authorize]
public class ComplaintController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly NotificationService _notif;

    public ComplaintController(AppDbContext db, UserManager<AppUser> userManager, NotificationService notif)
    {
        _db = db;
        _userManager = userManager;
        _notif = notif;
    }

    // POST /Complaint/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string targetType, string targetId, string? targetName,
        string reason, string? description)
    {
        var userId = _userManager.GetUserId(User)!;

        if (string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(reason))
        {
            TempData["Error"] = "Vui lòng chọn lý do báo cáo.";
            return RedirectBack();
        }

        // Chống gửi trùng: cùng người, cùng đối tượng, còn đang chờ xử lý.
        var dup = _db.Complaints.Any(c => c.ReporterId == userId
            && c.TargetType == targetType && c.TargetId == targetId && c.Status == "Pending");
        if (dup)
        {
            TempData["Error"] = "Bạn đã gửi báo cáo cho đối tượng này và đang được xử lý.";
            return RedirectBack();
        }

        _db.Complaints.Add(new Complaint
        {
            ReporterId = userId,
            TargetType = targetType,
            TargetId = targetId,
            TargetName = targetName,
            Reason = reason,
            Description = description,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // Báo cho tất cả admin (dùng hệ thống thông báo real-time).
        var admins = await _userManager.GetUsersInRoleAsync("Admin");
        var reporter = await _userManager.GetUserAsync(User);
        await _notif.NotifyManyAsync(
            admins.Select(a => a.Id),
            "🚩 Báo cáo mới",
            $"{reporter?.FullName ?? "Người dùng"} đã báo cáo {targetName ?? targetType}: {reason}.",
            "/Admin/Complaints");

        TempData["Success"] = "Đã gửi báo cáo. Cảm ơn bạn, đội ngũ quản trị sẽ xem xét.";
        return RedirectBack();
    }

    private IActionResult RedirectBack()
    {
        var referer = Request.Headers["Referer"].ToString();
        return string.IsNullOrEmpty(referer)
            ? RedirectToAction("Index", "Home")
            : Redirect(referer);
    }
}
