namespace TutorPlatform.Core.Models;

// Bài tập gia sư giao cho học viên (gắn với 1 buổi học).
public class Assignment
{
    public int Id { get; set; }

    public int BookingId { get; set; }
    public Booking? Booking { get; set; }

    public string TutorId { get; set; } = string.Empty;    // AppUser id của gia sư
    public string StudentId { get; set; } = string.Empty;  // AppUser id của học viên

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Phần nộp bài của học viên
    public string? SubmissionText { get; set; }
    public string? SubmissionFileUrl { get; set; }
    public DateTime? SubmittedAt { get; set; }

    // Phần chấm của gia sư
    public string? Grade { get; set; }      // VD "8/10" hoặc "Đạt"
    public string? Feedback { get; set; }
    public DateTime? GradedAt { get; set; }

    public string Status { get; set; } = "Assigned"; // Assigned | Submitted | Graded
}
