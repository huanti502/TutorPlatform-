namespace TutorPlatform.Core.Models;

public class Subject
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Level { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<TutorSubject> TutorSubjects { get; set; } = new List<TutorSubject>();
}

public class TutorSubject
{
    public int TutorProfileId { get; set; }
    public TutorProfile TutorProfile { get; set; } = null!;
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
}