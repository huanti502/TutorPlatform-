namespace TutorPlatform.Core.Models;

// Lớp cần gia sư: học viên/phụ huynh đăng nhu cầu, gia sư "đề nghị dạy".
public class ClassRequest
{
    public int Id { get; set; }

    public string StudentId { get; set; } = string.Empty;
    public AppUser? Student { get; set; }

    public string Title { get; set; } = string.Empty;         // VD: "Toán lớp 7 lên 8 - 2 buổi/tuần"
    public string Description { get; set; } = string.Empty;   // mô tả học sinh, yêu cầu gia sư, lịch học...

    public int? SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public string Mode { get; set; } = "Online";               // Online | Offline | Both
    public string? Location { get; set; }                      // khu vực (nếu học trực tiếp)
    public string Schedule { get; set; } = string.Empty;       // VD: "Tối T2, T4 từ 19h"
    public int SessionsPerWeek { get; set; } = 2;
    public decimal BudgetPerSession { get; set; }              // học phí đề xuất / buổi

    public string Status { get; set; } = "Open";               // Open | Matched | Closed
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ClassApplication> Applications { get; set; } = new List<ClassApplication>();
}

// Đề nghị dạy của gia sư cho một lớp.
public class ClassApplication
{
    public int Id { get; set; }

    public int ClassRequestId { get; set; }
    public ClassRequest? ClassRequest { get; set; }

    public int TutorProfileId { get; set; }
    public TutorProfile? TutorProfile { get; set; }

    public string? Message { get; set; }                       // lời nhắn của gia sư
    public decimal? ProposedRate { get; set; }                 // học phí gia sư đề xuất (nếu khác)

    public string Status { get; set; } = "Pending";            // Pending | Accepted | Rejected
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
