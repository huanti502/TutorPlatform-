namespace TutorPlatform.Core.Models;

// Yêu cầu rút tiền từ ví (chủ yếu cho gia sư). Tiền bị trừ ngay khi tạo yêu cầu,
// nếu admin từ chối thì hoàn lại vào ví.
public class WithdrawalRequest
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public AppUser? User { get; set; }

    public decimal Amount { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string BankAccount { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";   // Pending | Approved | Rejected
    public string? AdminNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? HandledAt { get; set; }
}
