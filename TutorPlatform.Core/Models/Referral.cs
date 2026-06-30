namespace TutorPlatform.Core.Models;

// Một lượt giới thiệu thành công: người giới thiệu (Referrer) -> người được giới thiệu (Referee).
public class Referral
{
    public int Id { get; set; }

    public string ReferrerId { get; set; } = string.Empty;
    public string RefereeId { get; set; } = string.Empty;

    public string ReferrerCouponCode { get; set; } = string.Empty;  // mã thưởng cho người giới thiệu
    public string RefereeCouponCode { get; set; } = string.Empty;   // mã thưởng cho người được giới thiệu

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
