namespace TutorPlatform.Core.Models;

public class Review
{
    public int Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public AppUser Student { get; set; } = null!;
    public int TutorProfileId { get; set; }
    public TutorProfile TutorProfile { get; set; } = null!;
    public int BookingId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ReviewReply? Reply { get; set; }
}

public class ReviewReply
{
    public int Id { get; set; }
    public int ReviewId { get; set; }
    public Review Review { get; set; } = null!;
    public string AuthorId { get; set; } = string.Empty;
    public AppUser Author { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}