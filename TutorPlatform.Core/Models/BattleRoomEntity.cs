namespace TutorPlatform.Core.Models;

public class BattleRoomEntity
{
    public int Id { get; set; }

    public string RoomId { get; set; } = "";

    public int SubjectId { get; set; }

    public string SubjectName { get; set; } = "";

    public string Level { get; set; } = "";

    public string Player1Id { get; set; } = "";

    public string Player1Name { get; set; } = "";

    public string Player2Id { get; set; } = "";

    public string Player2Name { get; set; } = "";

    public string? Player1ConnectionId { get; set; }

    public string? Player2ConnectionId { get; set; }

    public int Score1 { get; set; }

    public int Score2 { get; set; }

    public string Status { get; set; } = "Waiting";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? StartTime { get; set; }

    public DateTime? FinishedAt { get; set; }
}