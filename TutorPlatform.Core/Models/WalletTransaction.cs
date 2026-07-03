namespace TutorPlatform.Core.Models;

// #8: Giao dịch ví (số dư = tổng Amount của user)
public class WalletTransaction
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public decimal Amount { get; set; }                // + nạp/nhận, - trừ
    public string Type { get; set; } = "Deposit";      // Deposit, Hold, Release, Refund
    public string? Description { get; set; }
    public int? BookingId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
