namespace TutorPlatform.Core.Models;

// Nhật ký hoạt động của quản trị viên.
public class AuditLog
{
    public int Id { get; set; }

    public string ActorId { get; set; } = string.Empty;   // admin thực hiện
    public string ActorName { get; set; } = string.Empty; // tên hiển thị (snapshot)

    public string Action { get; set; } = string.Empty;      // vd: "Khoá tài khoản"
    public string? TargetType { get; set; }                 // vd: "User", "Tutor", "Coupon"
    public string? TargetName { get; set; }                 // đối tượng bị tác động
    public string? Details { get; set; }                    // mô tả thêm

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
