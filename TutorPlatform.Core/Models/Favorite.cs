namespace TutorPlatform.Core.Models;

// Gia sư mà học viên đã lưu (yêu thích)
public class Favorite
{
    public int Id { get; set; }

    // Học viên lưu
    public string StudentId { get; set; } = string.Empty;
    public AppUser? Student { get; set; }

    // Gia sư được lưu
    public int TutorProfileId { get; set; }
    public TutorProfile? TutorProfile { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
