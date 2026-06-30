namespace TutorPlatform.Core.Models;

// Lưu lịch sử giao dịch thanh toán (VNPay sandbox).
public class Payment
{
    public int Id { get; set; }

    public int BookingId { get; set; }
    public Booking? Booking { get; set; }

    public long OrderCode { get; set; }          // vnp_TxnRef - mã giao dịch duy nhất
    public decimal Amount { get; set; }          // số tiền (VND)
    public string Status { get; set; } = "Pending"; // Pending / Paid / Failed

    public string? TransactionNo { get; set; }   // vnp_TransactionNo từ VNPay
    public string? ResponseCode { get; set; }    // vnp_ResponseCode

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }

    public string? CouponCode { get; set; }              // mã giảm giá đã áp (nếu có)
    public decimal DiscountAmount { get; set; } = 0;     // số tiền đã giảm (VND)
}
