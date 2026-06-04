using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using TutorPlatform.Core.Models;
using TutorPlatform.Web.Services;
using System.Collections.Concurrent;

namespace TutorPlatform.Web.Hubs;

[Authorize]
public class BattleHub : Hub
{
    private readonly UserManager<AppUser> _userManager;
    private readonly XpService _xpService;

    // Lưu trạng thái các phòng battle trong memory (key = roomId)
    public static ConcurrentDictionary<string, BattleRoom> Rooms = new();

    public BattleHub(UserManager<AppUser> userManager, XpService xpService)
    {
        _userManager = userManager;
        _xpService = xpService;
    }

    // ── Người mời gửi lời thách đấu ──────────────────────────────
    public async Task Challenge(string opponentId, int subjectId, string subjectName, string level)
    {
        var challenger = await _userManager.GetUserAsync(Context.User!);
        if (challenger == null) return;

        var roomId = Guid.NewGuid().ToString("N")[..8];

        Rooms[roomId] = new BattleRoom
        {
            RoomId = roomId,
            SubjectId = subjectId,
            SubjectName = subjectName,
            Level = level,
            Player1Id = challenger.Id,
            Player1Name = challenger.FullName,
            Player1ConnectionId = Context.ConnectionId, // lưu ngay khi challenge
            Player2Id = opponentId,
            Status = "Waiting"
        };

        // Gửi lời mời tới đối thủ
        await Clients.User(opponentId).SendAsync("ReceiveBattleInvite", new
        {
            roomId,
            challengerName = challenger.FullName,
            challengerId = challenger.Id,
            subjectName,
            level
        });

        // Báo cho người mời biết đã gửi
        await Clients.Caller.SendAsync("ChallengeSent", new { roomId });
    }

    // ── Người được mời chấp nhận ──────────────────────────────────
    public async Task AcceptChallenge(string roomId)
    {
        if (!Rooms.TryGetValue(roomId, out var room)) return;

        var user = await _userManager.GetUserAsync(Context.User!);
        if (user == null || user.Id != room.Player2Id) return;

        room.Player2Name = user.FullName;
        room.Player2ConnectionId = Context.ConnectionId;
        room.Status = "Ready";

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        // Thêm player1 vào group nếu có connectionId
        if (room.Player1ConnectionId != null)
            await Groups.AddToGroupAsync(room.Player1ConnectionId, roomId);

        // Báo Player2 vào trang battle ngay
        await Clients.Caller.SendAsync("BattleStarting", new
        {
            roomId,
            subjectId = room.SubjectId,
            subjectName = room.SubjectName,
            level = room.Level,
            player1Name = room.Player1Name,
            player2Name = room.Player2Name
        });

        // Báo Player1 vào trang battle (dùng User() để đảm bảo nhận được)
        await Clients.User(room.Player1Id).SendAsync("BattleStarting", new
        {
            roomId,
            subjectId = room.SubjectId,
            subjectName = room.SubjectName,
            level = room.Level,
            player1Name = room.Player1Name,
            player2Name = room.Player2Name
        });
    }

    // ── Từ chối lời mời ───────────────────────────────────────────
    public async Task DeclineChallenge(string roomId)
    {
        if (!Rooms.TryGetValue(roomId, out var room)) return;
        Rooms.TryRemove(roomId, out _);
        await Clients.User(room.Player1Id).SendAsync("ChallengeDeclined");
    }

    // ── Player vào phòng, đăng ký connectionId ───────────────────
    public async Task JoinRoom(string roomId)
    {
        if (!Rooms.TryGetValue(roomId, out var room)) return;
        var userId = _userManager.GetUserId(Context.User!);

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        if (userId == room.Player1Id)
            room.Player1ConnectionId = Context.ConnectionId;
        else if (userId == room.Player2Id)
            room.Player2ConnectionId = Context.ConnectionId;

        // Nếu cả 2 đã vào → bắt đầu
        if (room.Player1ConnectionId != null && room.Player2ConnectionId != null
            && room.Status == "Ready")
        {
            room.Status = "Playing";
            room.StartTime = DateTime.UtcNow;
            await Clients.Group(roomId).SendAsync("BothReady");
        }
    }

    // ── Gửi câu trả lời ──────────────────────────────────────────
    public async Task SubmitAnswer(string roomId, int questionIndex, bool isCorrect)
    {
        if (!Rooms.TryGetValue(roomId, out var room)) return;
        var userId = _userManager.GetUserId(Context.User!);

        if (userId == room.Player1Id && isCorrect) room.Score1++;
        else if (userId == room.Player2Id && isCorrect) room.Score2++;

        // Push điểm live cho cả 2
        await Clients.Group(roomId).SendAsync("ScoreUpdate", new
        {
            score1 = room.Score1,
            score2 = room.Score2
        });
    }

    // ── Kết thúc trận (client gọi sau khi hết 60s) ───────────────
    public async Task FinishBattle(string roomId)
    {
        if (!Rooms.TryGetValue(roomId, out var room)) return;
        if (room.Status == "Finished") return; // chỉ xử lý 1 lần
        room.Status = "Finished";

        string winnerId, loserId, winnerName, result;
        if (room.Score1 > room.Score2)
        {
            winnerId = room.Player1Id; loserId = room.Player2Id;
            winnerName = room.Player1Name; result = "player1";
        }
        else if (room.Score2 > room.Score1)
        {
            winnerId = room.Player2Id; loserId = room.Player1Id;
            winnerName = room.Player2Name; result = "player2";
        }
        else
        {
            winnerId = ""; loserId = ""; winnerName = ""; result = "draw";
        }

        // Cộng XP cho người thắng
        int xpWon = 0, xpLost = 0;
        if (result != "draw")
        {
            xpWon = await _xpService.AwardXpAsync(winnerId, "quiz_pass");   // +30 XP
            xpLost = 10; // khuyến khích người thua (tự cộng thủ công)
            var loser = await _userManager.FindByIdAsync(loserId);
            if (loser != null) { loser.XpPoints += 10; await _userManager.UpdateAsync(loser); }
        }

        await Clients.Group(roomId).SendAsync("BattleResult", new
        {
            result,
            winnerName,
            score1 = room.Score1,
            score2 = room.Score2,
            xpWinner = xpWon,
            xpLoser = xpLost
        });

        Rooms.TryRemove(roomId, out _);
    }
    // Player1 broadcast câu hỏi cho cả phòng
    public async Task BroadcastQuestions(string roomId, string questionsJson)
    {
        await Clients.Group(roomId).SendAsync("ReceiveQuestions", questionsJson);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Nếu disconnect giữa chừng → báo đối thủ thắng
        var userId = _userManager.GetUserId(Context.User!);
        var room = Rooms.Values.FirstOrDefault(r =>
            (r.Player1Id == userId || r.Player2Id == userId) && r.Status == "Playing");

        if (room != null)
        {
            room.Status = "Finished";
            await Clients.Group(room.RoomId).SendAsync("OpponentLeft");
            Rooms.TryRemove(room.RoomId, out _);
        }

        await base.OnDisconnectedAsync(exception);
    }
}

// ── Model lưu trạng thái phòng trong RAM ─────────────────────────
public class BattleRoom
{
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
    public string Status { get; set; } = "Waiting"; // Waiting|Ready|Playing|Finished
    public DateTime StartTime { get; set; }
}