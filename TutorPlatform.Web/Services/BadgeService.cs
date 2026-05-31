using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.Hubs;

namespace TutorPlatform.Web.Services;

public class BadgeService
{
    private readonly AppDbContext _db;
    private readonly IHubContext<ChatHub> _hubContext; // ✅ Thêm field

    // ✅ Sửa constructor để Inject IHubContext
    public BadgeService(AppDbContext db, IHubContext<ChatHub> hubContext)
    {
        _db = db;
        _hubContext = hubContext;
    }

    /// Gọi hàm này ngay sau khi booking.Status = "Completed"
    public async Task CheckAndAwardAsync(int tutorProfileId)
    {
        var profile = await _db.TutorProfiles
            .Include(t => t.Bookings)
            .Include(t => t.ReceivedReviews)
            .Include(t => t.TutorSubjects)
            .Include(t => t.TutorBadges)
            .FirstOrDefaultAsync(t => t.Id == tutorProfileId);

        if (profile == null) return;

        var allBadges = await _db.Badges.ToListAsync();
        var earnedIds = profile.TutorBadges.Select(tb => tb.BadgeId).ToHashSet();

        int completedCount = profile.Bookings.Count(b => b.Status == "Completed");
        int reviewCount = profile.ReceivedReviews.Count;
        double avgRating = reviewCount > 0 ? profile.ReceivedReviews.Average(r => r.Rating) : 0;
        int subjectCount = profile.TutorSubjects.Count;
        decimal totalRevenue = profile.Bookings
            .Where(b => b.Status == "Completed")
            .Sum(b => (decimal)(b.EndTime - b.StartTime).TotalHours * profile.HourlyRate);

        var newBadges = new List<TutorBadge>();

        foreach (var badge in allBadges)
        {
            if (earnedIds.Contains(badge.Id)) continue;

            bool qualified = badge.Type switch
            {
                BadgeType.Sessions => completedCount >= badge.RequiredCount,
                BadgeType.Reviews => reviewCount >= badge.RequiredCount,
                BadgeType.Subjects => subjectCount >= badge.RequiredCount,
                BadgeType.Revenue => totalRevenue >= badge.RequiredCount * 1000,
                BadgeType.Rating => reviewCount >= 5 && avgRating * 10 >= badge.RequiredCount,
                _ => false
            };

            if (!qualified) continue;

            newBadges.Add(new TutorBadge
            {
                TutorProfileId = tutorProfileId,
                BadgeId = badge.Id,
                EarnedAt = DateTime.UtcNow
            });

            _db.Notifications.Add(new Notification
            {
                UserId = profile.UserId,
                Title = $"🏆 Huy hiệu mới: {badge.Name}",
                Content = $"Chúc mừng! Bạn vừa đạt \"{badge.Name}\". {badge.Description}",
                Link = "/Tutor/Dashboard"
            });
        }

        if (newBadges.Any())
        {
            _db.TutorBadges.AddRange(newBadges);
            await _db.SaveChangesAsync();

            // ✅ THÊM: Push từng huy hiệu mới lên realtime cho gia sư
            foreach (var tb in newBadges)
            {
                var badge = allBadges.First(b => b.Id == tb.BadgeId);
                await ChatHub.SendNotificationToUser(
                    _hubContext,
                    profile.UserId,
                    $"🏆 Huy hiệu mới: {badge.Icon} {badge.Name}",
                    badge.Description,
                    "/Tutor/Dashboard"
                );
            }
        }
    }
}