namespace TutorPlatform.Core.Models;

// Khiếu nại / tố cáo do người dùng gửi, admin kiểm duyệt.
public class Complaint
{
    public int Id { get; set; }

    public string ReporterId { get; set; } = string.Empty;   // người gửi khiếu nại
    public AppUser? Reporter { get; set; }

    public string TargetType { get; set; } = "Tutor";        // "Tutor" | "Review" | "User"
    public string TargetId { get; set; } = string.Empty;     // id đối tượng (lưu dạng text cho linh hoạt)
    public string? TargetName { get; set; }                  // tên hiển thị (snapshot lúc gửi)

    public string Reason { get; set; } = string.Empty;       // nhóm lý do
    public string? Description { get; set; }                 // mô tả chi tiết

    public string Status { get; set; } = "Pending";          // Pending | Resolved | Dismissed
    public string? AdminNote { get; set; }                   // ghi chú của admin khi xử lý

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? HandledAt { get; set; }
}
