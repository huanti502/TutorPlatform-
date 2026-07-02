using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;

namespace TutorPlatform.Web.Services;

public class RecommendationService
{
    private readonly AppDbContext _db;
    private readonly AIService _ai;

    public RecommendationService(AppDbContext db, AIService ai)
    {
        _db = db;
        _ai = ai;
    }

    public class TutorScore
    {
        public TutorProfile Profile { get; set; } = null!;
        public double Score { get; set; }
        public string Reason { get; set; } = string.Empty; // AI giải thích
        public double AvgRating { get; set; }
    }

    /// <summary>
    /// Gợi ý top N gia sư phù hợp nhất cho học viên dựa trên:
    /// - Lịch sử học (môn đã học, gia sư đã từng đánh giá tốt)
    /// - Collaborative filtering đơn giản (học viên tương tự chọn ai)
    /// - Scoring nhiều chiều
    /// </summary>
    public async Task<List<TutorScore>> GetRecommendationsAsync(
        string studentId, int topN = 6)
    {
        // 1. Lấy lịch sử học của student
        var myBookings = await _db.Bookings
            .Where(b => b.StudentId == studentId)
            .Include(b => b.Subject)
            .Include(b => b.TutorProfile)
            .ToListAsync();

        var mySubjectIds = myBookings
            .Select(b => b.SubjectId).Distinct().ToHashSet();

        var myTutorIds = myBookings
            .Select(b => b.TutorProfileId).Distinct().ToHashSet();

        // 2. Môn học tôi đã đánh giá tốt (≥4 sao) → tìm thêm gia sư cùng môn
        var goodReviews = await _db.Reviews
            .Where(r => r.StudentId == studentId && r.Rating >= 4)
            .Include(r => r.TutorProfile).ThenInclude(t => t.TutorSubjects)
            .ToListAsync();

        var preferredSubjects = goodReviews
            .SelectMany(r => r.TutorProfile.TutorSubjects.Select(ts => ts.SubjectId))
            .Distinct().ToHashSet();

        // 3. Collaborative filtering: tìm học viên "tương tự"tôi
        //    (cùng học những gia sư tôi đã học) → xem họ học thêm ai
        var similarStudentIds = await _db.Bookings
            .Where(b => myTutorIds.Contains(b.TutorProfileId) && b.StudentId != studentId)
            .Select(b => b.StudentId)
            .Distinct()
            .ToListAsync();

        var collaboTutorIds = await _db.Bookings
            .Where(b => similarStudentIds.Contains(b.StudentId) &&
                        !myTutorIds.Contains(b.TutorProfileId))
            .Select(b => b.TutorProfileId)
            .Distinct()
            .ToListAsync();

        // 4. Lấy tất cả gia sư đã duyệt
        var allTutors = await _db.TutorProfiles
            .Include(t => t.User)
            .Include(t => t.TutorSubjects).ThenInclude(ts => ts.Subject)
            .Include(t => t.ReceivedReviews)
            .Include(t => t.Bookings)
            .Include(t => t.TutorBadges).ThenInclude(tb => tb.Badge)
            .Where(t => t.IsApproved)
            .ToListAsync();

        // 5. Tính điểm cho từng gia sư
        var scored = allTutors.Select(t =>
        {
            double score = 0;
            var reasons = new List<string>();
            var avg = t.ReceivedReviews.Any()
                ? t.ReceivedReviews.Average(r => r.Rating) : 0;

            // +40: Dạy môn tôi đã học hoặc môn tôi thích
            var tSubIds = t.TutorSubjects.Select(ts => ts.SubjectId).ToHashSet();
            if (tSubIds.Intersect(preferredSubjects).Any())
            { score += 40; reasons.Add("dạy môn bạn yêu thích"); }
            else if (tSubIds.Intersect(mySubjectIds).Any())
            { score += 25; reasons.Add("dạy môn bạn đang học"); }

            // +25: Rating cao
            if (avg >= 4.8) { score += 25; reasons.Add("rating xuất sắc "); }
            else if (avg >= 4.5) { score += 18; reasons.Add("rating rất tốt"); }
            else if (avg >= 4.0) { score += 10; }

            // +20: Nhiều buổi hoàn thành = uy tín
            var done = t.Bookings.Count(b => b.Status == "Completed");
            if (done >= 50) { score += 20; reasons.Add("kinh nghiệm dày dạn"); }
            else if (done >= 10) { score += 12; }
            else if (done >= 1) { score += 5; }

            // +15: Collaborative filtering
            if (collaboTutorIds.Contains(t.Id))
            { score += 15; reasons.Add("học viên tương tự đã chọn"); }

            // +10: Nhiều huy hiệu
            if (t.TutorBadges.Count >= 3) { score += 10; reasons.Add("có nhiều huy hiệu"); }
            else if (t.TutorBadges.Any()) { score += 5; }

            // -5: Gia sư đã từng học (không recommend lại nếu đã biết)
            if (myTutorIds.Contains(t.Id)) score -= 5;

            var reason = reasons.Any()
                ? "Phù hợp vì: " + string.Join(", ", reasons)
                : "Gia sư chất lượng";

            return new TutorScore
            {
                Profile = t,
                Score = score,
                Reason = reason,
                AvgRating = avg
            };
        })
        .OrderByDescending(x => x.Score)
        .Take(topN)
        .ToList();

        return scored;
    }
}