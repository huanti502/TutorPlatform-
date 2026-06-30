namespace TutorPlatform.Core.Models;

// Gói nhiều buổi do gia sư định nghĩa (vd: 10 buổi giá ưu đãi).
public class LessonPackage
{
    public int Id { get; set; }

    public int TutorProfileId { get; set; }
    public TutorProfile? TutorProfile { get; set; }

    public string Name { get; set; } = string.Empty;
    public int SessionCount { get; set; }     // số buổi trong gói
    public decimal Price { get; set; }        // tổng giá gói (VND)

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Lượt mua gói của học viên (credit số buổi còn lại).
public class PackagePurchase
{
    public int Id { get; set; }

    public int LessonPackageId { get; set; }
    public LessonPackage? LessonPackage { get; set; }

    public string StudentId { get; set; } = string.Empty;
    public int TutorProfileId { get; set; }

    public int TotalSessions { get; set; }
    public int RemainingSessions { get; set; }
    public decimal PricePaid { get; set; }

    public long OrderCode { get; set; }       // vnp_TxnRef
    public string Status { get; set; } = "Pending"; // Pending | Active | Used

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ActivatedAt { get; set; }
}
