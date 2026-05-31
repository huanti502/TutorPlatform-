namespace TutorPlatform.Core.Models;

public class QuizAttempt
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public AppUser User { get; set; } = null!;

    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    public string Level { get; set; } = string.Empty;   // "Cơ bản" / "Trung bình" / "Nâng cao"
    public int Score { get; set; }                      // Số câu đúng
    public int TotalQuestions { get; set; }             // Tổng số câu (mặc định 10)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}