namespace TutorPlatform.Core.Models;

public class Certificate
{
    public int Id { get; set; }
    public int TutorProfileId { get; set; }
    public TutorProfile TutorProfile { get; set; } = null!;
    public string Title { get; set; } = string.Empty; // "Bằng Thạc sĩ Toán"
    public string FilePath { get; set; } = string.Empty; // đường dẫn ảnh/pdf
    public string FileType { get; set; } = string.Empty; // "image" | "pdf"
    public CertificateType Type { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public bool IsVerified { get; set; } = false; // Admin xác nhận
}

public enum CertificateType
{
    Degree,        // Bằng cấp
    Certificate,   // Chứng chỉ
    FacePhoto      // Ảnh khuôn mặt để xác minh danh tính
}