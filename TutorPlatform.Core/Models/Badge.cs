namespace TutorPlatform.Core.Models;

public class Badge
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;   // emoji: "🏆"
    public string Color { get; set; } = "#FFD700";
    public BadgeType Type { get; set; }
    public int RequiredCount { get; set; }

    public ICollection<TutorBadge> TutorBadges { get; set; } = new List<TutorBadge>();
}

public enum BadgeType
{
    Sessions,   // số buổi hoàn thành
    Rating,     // điểm trung bình (x10, vd 45 = 4.5 sao)
    Reviews,    // số lượt đánh giá
    Subjects,   // số môn học
    Revenue     // doanh thu (nghìn VNĐ)
}

public class TutorBadge
{
    public int Id { get; set; }
    public int TutorProfileId { get; set; }
    public TutorProfile TutorProfile { get; set; } = null!;
    public int BadgeId { get; set; }
    public Badge Badge { get; set; } = null!;
    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;
}