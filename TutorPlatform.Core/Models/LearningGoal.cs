namespace TutorPlatform.Core.Models;

// Mục tiêu học tập của học viên (#21)
public class LearningGoal
{
    public int Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime? TargetDate { get; set; }
    public int Progress { get; set; } = 0; // 0-100 (%)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
