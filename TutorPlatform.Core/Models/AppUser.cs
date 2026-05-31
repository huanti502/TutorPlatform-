using Microsoft.AspNetCore.Identity;

namespace TutorPlatform.Core.Models;

public class AppUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = "Student";
    public bool IsLocked { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public TutorProfile? TutorProfile { get; set; }
    public ICollection<Booking> StudentBookings { get; set; } = new List<Booking>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Message> SentMessages { get; set; } = new List<Message>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public int XpPoints { get; set; } = 0;
    public string XpLevel { get; set; } = "Mới bắt đầu";  // tính tự động
}