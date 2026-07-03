namespace TutorPlatform.Core.Models;

// #18: từ/cụm từ cấm do admin quản lý
public class BannedWord
{
    public int Id { get; set; }
    public string Word { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
