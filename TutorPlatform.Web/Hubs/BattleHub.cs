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

    public static ConcurrentDictionary<string, BattleRoom> Rooms = new();

    public BattleHub(UserManager<AppUser> userManager, XpService xpService)
    {
        _userManager = userManager;
        _xpService = xpService;
    }

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
            Player1ConnectionId = null,
            Player2Id = opponentId,
            Status = "Waiting"
        };

        await Clients.User(opponentId).SendAsync("ReceiveBattleInvite", new
        {
            roomId,
            challengerName = challenger.FullName,
            challengerId = challenger.Id,
            subjectName,
            level
        });

        await Clients.Caller.SendAsync("ChallengeSent", new { roomId });
    }

    public async Task AcceptChallenge(string roomId)
    {
        if (!Rooms.TryGetValue(roomId, out var room)) return;

        var user = await _userManager.GetUserAsync(Context.User!);
        if (user == null || user.Id != room.Player2Id) return;

        room.Player2Name = user.FullName;
        room.Status = "Ready";

        await Clients.Caller.SendAsync("BattleStarting", new
        {
            roomId,
            subjectId = room.SubjectId,
            subjectName = room.SubjectName,
            level = room.Level,
            player1Id = room.Player1Id,
            player1Name = room.Player1Name,
            player2Name = room.Player2Name
        });

        await Clients.User(room.Player1Id).SendAsync("BattleStarting", new
        {
            roomId,
            subjectId = room.SubjectId,
            subjectName = room.SubjectName,
            level = room.Level,
            player1Id = room.Player1Id,
            player1Name = room.Player1Name,
            player2Name = room.Player2Name
        });
    }

    public async Task DeclineChallenge(string roomId)
    {
        if (!Rooms.TryGetValue(roomId, out var room)) return;
        Rooms.TryRemove(roomId, out _);
        await Clients.User(room.Player1Id).SendAsync("ChallengeDeclined");
    }

    // 🚀 FIX: Nhận clientUserId trực tiếp từ JS để đảm bảo 100% khớp ID
    public async Task JoinRoom(string roomId, string clientUserId)
    {
        Console.WriteLine($"[BATTLE] Tín hiệu JoinRoom từ ID: {clientUserId} vào phòng {roomId}");

        if (!Rooms.TryGetValue(roomId, out var room))
        {
            Console.WriteLine($"[BATTLE] Lỗi: Không tìm thấy phòng {roomId}!");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        if (string.Equals(clientUserId, room.Player1Id, StringComparison.OrdinalIgnoreCase))
        {
            room.Player1ConnectionId = Context.ConnectionId;
            room.Player1Joined = true;
            Console.WriteLine($"[BATTLE] Player 1 ({room.Player1Name}) đã vào phòng.");
        }
        else if (string.Equals(clientUserId, room.Player2Id, StringComparison.OrdinalIgnoreCase))
        {
            room.Player2ConnectionId = Context.ConnectionId;
            room.Player2Joined = true;
            Console.WriteLine($"[BATTLE] Player 2 ({room.Player2Name}) đã vào phòng.");
        }

        if (room.Status == "Ready" || room.Status == "Playing")
        {
            await Clients.Caller.SendAsync("BattleStarting", new
            {
                roomId,
                subjectId = room.SubjectId,
                subjectName = room.SubjectName,
                level = room.Level,
                player1Id = room.Player1Id,
                player1Name = room.Player1Name,
                player2Name = room.Player2Name
            });
        }

        if (room.Player1Joined && room.Player2Joined && room.Status == "Ready")
        {
            Console.WriteLine($"[BATTLE] Thành công! Cả 2 đã vào phòng. Bắn tín hiệu BothReady.");
            room.Status = "Playing";
            room.StartTime = DateTime.UtcNow;
            await Clients.Group(roomId).SendAsync("BothReady");
        }
    }

    public async Task SubmitAnswer(string roomId, int questionIndex, bool isCorrect)
    {
        if (!Rooms.TryGetValue(roomId, out var room)) return;
        var userId = _userManager.GetUserId(Context.User!);

        if (userId == room.Player1Id && isCorrect) room.Score1++;
        else if (userId == room.Player2Id && isCorrect) room.Score2++;

        await Clients.Group(roomId).SendAsync("ScoreUpdate", new { score1 = room.Score1, score2 = room.Score2 });
    }

    public async Task FinishBattle(string roomId)
    {
        if (!Rooms.TryGetValue(roomId, out var room)) return;
        if (room.Status == "Finished") return;
        room.Status = "Finished";

        string winnerId = "", loserId = "", winnerName = "", result = "draw";
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

        int xpWon = 0, xpLost = 0;
        if (result != "draw")
        {
            xpWon = await _xpService.AwardXpAsync(winnerId, "quiz_pass");
            xpLost = 10;
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

    public async Task BroadcastQuestions(string roomId, string questionsJson)
    {
        await Clients.Group(roomId).SendAsync("ReceiveQuestions", questionsJson);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // 🚀 FIX: Dùng ConnectionId để tìm phòng, tránh việc kết nối cũ ngắt làm bay luôn phòng mới
        var room = Rooms.Values.FirstOrDefault(r =>
            (r.Player1ConnectionId == Context.ConnectionId || r.Player2ConnectionId == Context.ConnectionId)
            && r.Status == "Playing");

        if (room != null)
        {
            Console.WriteLine($"[BATTLE] Một người chơi đã thoát. Hủy phòng {room.RoomId}");
            room.Status = "Finished";
            await Clients.Group(room.RoomId).SendAsync("OpponentLeft");
            Rooms.TryRemove(room.RoomId, out _);
        }

        await base.OnDisconnectedAsync(exception);
    }
}

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
    public bool Player1Joined { get; set; } = false;
    public bool Player2Joined { get; set; } = false;
    public int Score1 { get; set; }
    public int Score2 { get; set; }
    public string Status { get; set; } = "Waiting";
    public DateTime StartTime { get; set; }
}