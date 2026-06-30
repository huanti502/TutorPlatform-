namespace TutorPlatform.Core.Models;

public class Booking
{
    public int Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public AppUser Student { get; set; } = null!;
    public int TutorProfileId { get; set; }
    public TutorProfile TutorProfile { get; set; } = null!;
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string TeachingMode { get; set; } = "Online";
    public string Status { get; set; } = "Pending";
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsPaid { get; set; } = false;

    public bool ReminderSent { get; set; } = false; // Đã gửi email nhắc lịch chưa (BackgroundService)
    
    public string? MeetingRoomId { get; set; } // Jitsi room ID tự động tạo
}
