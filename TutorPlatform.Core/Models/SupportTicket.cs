namespace TutorPlatform.Core.Models;

// Yêu cầu hỗ trợ / liên hệ gửi cho admin (hỗ trợ cả khách chưa đăng nhập).
public class SupportTicket
{
    public int Id { get; set; }

    public string? UserId { get; set; }                       // null nếu là khách vãng lai
    public AppUser? User { get; set; }

    public string FullName { get; set; } = string.Empty;      // tên người gửi (snapshot)
    public string Email { get; set; } = string.Empty;         // email liên hệ lại
    public string? Phone { get; set; }                        // SĐT (tuỳ chọn)

    public string Category { get; set; } = "Khác";            // Lỗi hệ thống | Thanh toán | Tài khoản | Khiếu nại | Góp ý | Khác
    public string Subject { get; set; } = string.Empty;       // tiêu đề ngắn
    public string Content { get; set; } = string.Empty;       // nội dung chi tiết

    public string Status { get; set; } = "Open";              // Open | InProgress | Resolved | Closed
    public string? AdminReply { get; set; }                   // nội dung phản hồi của admin
    public string? HandledById { get; set; }                  // admin xử lý

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RepliedAt { get; set; }
}
