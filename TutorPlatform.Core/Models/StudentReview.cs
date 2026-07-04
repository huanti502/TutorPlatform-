namespace TutorPlatform.Core.Models;

// Gia sư đánh giá học viên sau buổi học hoàn thành (đánh giá 2 chiều).
public class StudentReview
{
    public int Id { get; set; }

    public int BookingId { get; set; }
    public Booking? Booking { get; set; }

    public int TutorProfileId { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public AppUser? Student { get; set; }

    public int Rating { get; set; }                   // 1..5
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
