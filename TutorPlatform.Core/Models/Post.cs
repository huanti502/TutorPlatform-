namespace TutorPlatform.Core.Models;

public class Post
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public AppUser Author { get; set; } = null!;
    public int Views { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? ImageUrl { get; set; }
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    
    public ICollection<PostLike> Likes { get; set; } = new List<PostLike>();
    public bool IsPublished { get; set; } = false;
    public string? Summary { get; set; } // Thêm dòng này vào
}


public class PostLike
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public Post Post { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public AppUser User { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}