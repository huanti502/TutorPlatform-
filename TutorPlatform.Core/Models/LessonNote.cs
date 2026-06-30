namespace TutorPlatform.Core.Models;

// Ghi chú nội dung 1 buổi học (gia sư viết), kèm bản tóm tắt do AI tạo.
public class LessonNote
{
    public int Id { get; set; }

    public int BookingId { get; set; }
    public Booking? Booking { get; set; }

    public string TutorId { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;

    public string? Content { get; set; }     // ghi chú gốc của gia sư
    public string? AiSummary { get; set; }   // bản tóm tắt do AI tạo

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
