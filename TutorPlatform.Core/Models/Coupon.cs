namespace TutorPlatform.Core.Models;

// Mã giảm giá áp dụng khi thanh toán booking.
public class Coupon
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;       // VD: GIAM50K, WELCOME (luôn lưu in hoa)

    public string DiscountType { get; set; } = "Amount";   // "Amount" = giảm số tiền | "Percent" = giảm %
    public decimal DiscountValue { get; set; }             // số tiền VND, hoặc % (0–100)

    public decimal? MaxDiscount { get; set; }              // trần giảm tối đa (chỉ dùng cho Percent)
    public decimal MinOrder { get; set; } = 0;             // đơn tối thiểu để được áp

    public DateTime? ExpiresAt { get; set; }               // null = không hết hạn
    public int UsageLimit { get; set; } = 0;               // 0 = không giới hạn lượt
    public int UsedCount { get; set; } = 0;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
