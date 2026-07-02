using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TutorPlatform.Core.Models;


public class TutorProfile
{
    [Key] // Hãy đảm bảo có annotation này hoặc nó được cấu hình trong DbContext
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public AppUser User { get; set; } = null!;

    public string Education { get; set; } = string.Empty;
    public int ExperienceYears { get; set; }
    public string TeachingArea { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal HourlyRate { get; set; }

    public string TeachingMode { get; set; } = "Both";
    public string? Bio { get; set; }
    public bool IsApproved { get; set; } = false;

    // Xác thực khuôn mặt (chống giả mạo gia sư)
    public string? FaceDescriptor { get; set; }        // "vân" khuôn mặt gốc (128 số, JSON)
    public bool FaceVerified { get; set; } = false;    // đã xác thực khớp chưa
    public DateTime? FaceVerifiedAt { get; set; }      // lần xác thực gần nhất

    // Các danh sách liên kết (Navigation properties)
    public ICollection<TutorSubject> TutorSubjects { get; set; } = new List<TutorSubject>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Review> ReceivedReviews { get; set; } = new List<Review>();
    public ICollection<TutorAvailability> Availabilities { get; set; } = new List<TutorAvailability>();
    public ICollection<TutorBadge> TutorBadges { get; set; } = new List<TutorBadge>();
    public ICollection<Certificate> Certificates { get; set; } = new List<Certificate>();
}