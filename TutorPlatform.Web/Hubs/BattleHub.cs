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

    // Lưu trạng thái các phòng battle trong RAM
    public static ConcurrentDictionary<string, BattleRoom> Rooms = new();

    public BattleHub(UserManager<AppUser> userManager, XpService xpService)
    {
        _userManager = userManager;
        _xpService = xpService;
    }

    // Người mời gửi lời thách đấu
    public async Task Challenge(string opponentId, int subjectId, string subjectName, string level)
    {
        var challenger = await _userManager.GetUserAsync(Context.User!);
        if (challenger == null)
        {
            await Clients.Caller.SendAsync("BattleError", "Không tìm thấy tài khoản người gửi.");
            return;
        }

        if (string.IsNullOrWhiteSpace(opponentId))
        {
            await Clients.Caller.SendAsync("BattleError", "Không tìm thấy đối thủ.");
            return;
        }

        var roomId = Guid.NewGuid().ToString("N")[..8];

        Rooms[roomId] = new BattleRoom
        {
            RoomId = roomId,
            SubjectId = subjectId,
            SubjectName = subjectName,
            Level = level,
            Player1Id = challenger.Id,
            Player1Name = challenger.FullName,
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

    // Người được mời chấp nhận
    public async Task AcceptChallenge(string roomId)
    {
        if (!Rooms.TryGetValue(roomId, out var room))
        {
            await Clients.Caller.SendAsync("BattleError", "Không tìm thấy phòng Battle.");
            return;
        }

        var user = await _userManager.GetUserAsync(Context.User!);
        if (user == null)
        {
            await Clients.Caller.SendAsync("BattleError", "Không tìm thấy tài khoản người nhận.");
            return;
        }

        if (user.Id != room.Player2Id)
        {
            await Clients.Caller.SendAsync("BattleError", "Bạn không phải người được mời vào trận này.");
            return;
        }

        room.Player2Name = user.FullName;
        room.Status = "Ready";

        await Clients.User(room.Player1Id).SendAsync("ChallengeAccepted", new { roomId });
        await Clients.Caller.SendAsync("ChallengeAccepted", new { roomId });
    }

    // Từ chối lời mời
    public async Task DeclineChallenge(string roomId)
    {
        if (!Rooms.TryGetValue(roomId, out var room))
        {
            await Clients.Caller.SendAsync("BattleError", "Không tìm thấy phòng Battle.");
            return;
        }

        Rooms.TryRemove(roomId, out _);
        await Clients.User(room.Player1Id).SendAsync("ChallengeDeclined");
    }

    // Người chơi vào phòng
    public async Task JoinRoom(string roomId)
    {
        if (!Rooms.TryGetValue(roomId, out var room))
        {
            await Clients.Caller.SendAsync("RoomError", "Không tìm thấy phòng Battle. Vui lòng tạo trận mới.");
            return;
        }

        var userId = _userManager.GetUserId(Context.User!);

        if (userId != room.Player1Id && userId != room.Player2Id)
        {
            await Clients.Caller.SendAsync("RoomError", "Bạn không thuộc trận đấu này.");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        if (userId == room.Player1Id)
        {
            room.Player1ConnectionId = Context.ConnectionId;
        }
        else if (userId == room.Player2Id)
        {
            room.Player2ConnectionId = Context.ConnectionId;
        }

        await Clients.Caller.SendAsync("RoomInfo", new
        {
            roomId = room.RoomId,
            subjectId = room.SubjectId,
            subjectName = room.SubjectName,
            level = room.Level,
            player1Id = room.Player1Id,
            player1Name = room.Player1Name,
            player2Id = room.Player2Id,
            player2Name = room.Player2Name,
            status = room.Status
        });

        if (room.Player1ConnectionId != null &&
            room.Player2ConnectionId != null &&
            room.Status == "Ready")
        {
            room.Status = "Playing";
            room.StartTime = DateTime.UtcNow;

            await Clients.Group(roomId).SendAsync("RoomInfo", new
            {
                roomId = room.RoomId,
                subjectId = room.SubjectId,
                subjectName = room.SubjectName,
                level = room.Level,
                player1Id = room.Player1Id,
                player1Name = room.Player1Name,
                player2Id = room.Player2Id,
                player2Name = room.Player2Name,
                status = room.Status
            });

            await Clients.Group(roomId).SendAsync("BothReady");
        }
    }

    // Gửi câu trả lời
    public async Task SubmitAnswer(string roomId, int questionIndex, bool isCorrect)
    {
        if (!Rooms.TryGetValue(roomId, out var room)) return;

        var userId = _userManager.GetUserId(Context.User!);

        if (userId == room.Player1Id && isCorrect)
        {
            room.Score1++;
        }
        else if (userId == room.Player2Id && isCorrect)
        {
            room.Score2++;
        }

        await Clients.Group(roomId).SendAsync("ScoreUpdate", new
        {
            score1 = room.Score1,
            score2 = room.Score2
        });
    }

    // Kết thúc trận
    public async Task FinishBattle(string roomId)
    {
        if (!Rooms.TryGetValue(roomId, out var room)) return;
        if (room.Status == "Finished") return;

        room.Status = "Finished";

        string winnerId = "";
        string loserId = "";
        string winnerName = "";
        string result;

        if (room.Score1 > room.Score2)
        {
            winnerId = room.Player1Id;
            loserId = room.Player2Id;
            winnerName = room.Player1Name;
            result = "player1";
        }
        else if (room.Score2 > room.Score1)
        {
            winnerId = room.Player2Id;
            loserId = room.Player1Id;
            winnerName = room.Player2Name;
            result = "player2";
        }
        else
        {
            result = "draw";
        }

        int xpWon = 0;
        int xpLost = 0;

        if (result != "draw")
        {
            xpWon = await _xpService.AwardXpAsync(winnerId, "quiz_pass");
            xpLost = 10;

            var loser = await _userManager.FindByIdAsync(loserId);
            if (loser != null)
            {
                loser.XpPoints += 10;
                await _userManager.UpdateAsync(loser);
            }
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

    // Player 1 gửi câu hỏi cho cả phòng
    public async Task BroadcastQuestions(string roomId, string questionsJson)
    {
        if (!Rooms.ContainsKey(roomId)) return;

        await Clients.Group(roomId).SendAsync("ReceiveQuestions", questionsJson);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = _userManager.GetUserId(Context.User!);

        var room = Rooms.Values.FirstOrDefault(r =>
            (r.Player1Id == userId || r.Player2Id == userId) &&
            r.Status == "Playing");

        if (room != null)
        {
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

    public int Score1 { get; set; }
    public int Score2 { get; set; }

    public string Status { get; set; } = "Waiting";
    public DateTime StartTime { get; set; }
}