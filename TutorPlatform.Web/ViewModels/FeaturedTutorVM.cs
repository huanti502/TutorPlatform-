namespace TutorPlatform.Web.ViewModels;

public class FeaturedTutorVM
{
    public int TutorProfileId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Education { get; set; } = string.Empty;
    public decimal HourlyRate { get; set; }
    public double AvgRating { get; set; }
    public int ReviewCount { get; set; }
    public int CompletedSessions { get; set; }
    public List<string> Subjects { get; set; } = new();
    public double Score { get; set; }
    public bool IsTutorOfMonth { get; set; }
}
