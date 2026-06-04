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

        if (challenger.Id == opponentId)
        {
            await Clients.Caller.SendAsync("BattleError", "Không thể tự thách đấu chính mình.");
            return;
        }

        var roomId = Guid.NewGuid().ToString("N")[..8];

        var room = new BattleRoom
        {
            RoomId = roomId,
            SubjectId = subjectId,
            SubjectName = subjectName,
            Level = level,
            Player1Id = challenger.Id,
            Player1Name = string.IsNullOrWhiteSpace(challenger.FullName) ? challenger.Email ?? "Player 1" : challenger.FullName,
            Player2Id = opponentId,
            Player2Name = "",
            Status = "Waiting",
            CreatedAt = DateTime.UtcNow
        };

        Rooms[roomId] = room;

        await Clients.User(opponentId).SendAsync("ReceiveBattleInvite", new
        {
            roomId = room.RoomId,
            challengerName = room.Player1Name,
            challengerId = room.Player1Id,
            subjectId = room.SubjectId,
            subjectName = room.SubjectName,
            level = room.Level
        });

        await Clients.Caller.SendAsync("ChallengeSent", new
        {
            roomId = room.RoomId
        });
    }

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

        room.Player2Name = string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? "Player 2" : user.FullName;
        room.Status = "Ready";

        await Clients.User(room.Player1Id).SendAsync("ChallengeAccepted", new
        {
            roomId = room.RoomId
        });

        await Clients.Caller.SendAsync("ChallengeAccepted", new
        {
            roomId = room.RoomId
        });
    }

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

    public async Task JoinRoom(string roomId)
    {
        if (!Rooms.TryGetValue(roomId, out var room))
        {
            await Clients.Caller.SendAsync("RoomError", "Không tìm thấy phòng Battle. Vui lòng tạo trận mới.");
            return;
        }

        var userId = _userManager.GetUserId(Context.User!);

        if (string.IsNullOrWhiteSpace(userId))
        {
            await Clients.Caller.SendAsync("RoomError", "Bạn chưa đăng nhập.");
            return;
        }

        if (userId != room.Player1Id && userId != room.Player2Id)
        {
            await Clients.Caller.SendAsync("RoomError", "Bạn không thuộc trận đấu này.");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomId);

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

        if (!string.IsNullOrWhiteSpace(room.Player1ConnectionId) &&
            !string.IsNullOrWhiteSpace(room.Player2ConnectionId) &&
            room.Status == "Ready")
        {
            room.Status = "Playing";
            room.StartTime = DateTime.UtcNow;

            await Clients.Group(room.RoomId).SendAsync("RoomInfo", new
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

            await Clients.Group(room.RoomId).SendAsync("BothReady");
        }
    }

    public async Task SubmitAnswer(string roomId, int questionIndex, bool isCorrect)
    {
        if (!Rooms.TryGetValue(roomId, out var room))
        {
            return;
        }

        if (room.Status != "Playing")
        {
            return;
        }

        var userId = _userManager.GetUserId(Context.User!);

        if (userId == room.Player1Id && isCorrect)
        {
            room.Score1++;
        }
        else if (userId == room.Player2Id && isCorrect)
        {
            room.Score2++;
        }

        await Clients.Group(room.RoomId).SendAsync("ScoreUpdate", new
        {
            score1 = room.Score1,
            score2 = room.Score2
        });
    }

    public async Task FinishBattle(string roomId)
    {
        if (!Rooms.TryGetValue(roomId, out var room))
        {
            return;
        }

        if (room.Status == "Finished")
        {
            return;
        }

        room.Status = "Finished";

        string result;
        string winnerId = "";
        string loserId = "";
        string winnerName = "";

        if (room.Score1 > room.Score2)
        {
            result = "player1";
            winnerId = room.Player1Id;
            loserId = room.Player2Id;
            winnerName = room.Player1Name;
        }
        else if (room.Score2 > room.Score1)
        {
            result = "player2";
            winnerId = room.Player2Id;
            loserId = room.Player1Id;
            winnerName = room.Player2Name;
        }
        else
        {
            result = "draw";
        }

        int xpWinner = 0;
        int xpLoser = 0;

        if (result != "draw")
        {
            xpWinner = await _xpService.AwardXpAsync(winnerId, "quiz_pass");
            xpLoser = 10;

            var loser = await _userManager.FindByIdAsync(loserId);
            if (loser != null)
            {
                loser.XpPoints += xpLoser;
                await _userManager.UpdateAsync(loser);
            }
        }

        await Clients.Group(room.RoomId).SendAsync("BattleResult", new
        {
            result,
            winnerName,
            score1 = room.Score1,
            score2 = room.Score2,
            xpWinner,
            xpLoser
        });

        Rooms.TryRemove(room.RoomId, out _);
    }

    public async Task BroadcastQuestions(string roomId, string questionsJson)
    {
        if (!Rooms.TryGetValue(roomId, out var room))
        {
            await Clients.Caller.SendAsync("RoomError", "Không tìm thấy phòng Battle.");
            return;
        }

        if (room.Status != "Playing")
        {
            await Clients.Caller.SendAsync("RoomError", "Trận đấu chưa sẵn sàng.");
            return;
        }

        await Clients.Group(room.RoomId).SendAsync("ReceiveQuestions", questionsJson);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = _userManager.GetUserId(Context.User!);

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var room = Rooms.Values.FirstOrDefault(r =>
                (r.Player1Id == userId || r.Player2Id == userId) &&
                r.Status == "Playing");

            if (room != null)
            {
                room.Status = "Finished";
                await Clients.Group(room.RoomId).SendAsync("OpponentLeft");
                Rooms.TryRemove(room.RoomId, out _);
            }
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

    public DateTime CreatedAt { get; set; }

    public DateTime StartTime { get; set; }
}