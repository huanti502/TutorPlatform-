namespace TutorPlatform.Core.Models;

public class Document
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;  // đường dẫn vật lý
    public string FileName { get; set; } = string.Empty;  // tên gốc
    public string FileType { get; set; } = string.Empty;  // "pdf","doc","img"...
    public long FileSize { get; set; }                   // bytes
    public string UploaderId { get; set; } = string.Empty;
    public AppUser Uploader { get; set; } = null!;
    public int? BookingId { get; set; }   // gắn với buổi học cụ thể (tuỳ chọn)
    public Booking? Booking { get; set; }
    public bool IsPublic { get; set; } = false;  // public = ai cũng xem được
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int DownloadCount { get; set; } = 0;

    public string? AISummary { get; set; }
    public DateTime? SummarizedAt { get; set; }
}