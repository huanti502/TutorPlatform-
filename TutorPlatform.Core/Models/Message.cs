namespace TutorPlatform.Core.Models;

public class Message
{
    public int Id { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public AppUser Sender { get; set; } = null!;
    public string ReceiverId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsRead { get; set; } = false;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}