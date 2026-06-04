using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.Services;

namespace TutorPlatform.Web.Hubs;

[Authorize]
public class BattleHub : Hub
{
    private readonly UserManager<AppUser> _userManager;
    private readonly XpService _xpService;
    private readonly AppDbContext _db;

    public BattleHub(
        UserManager<AppUser> userManager,
        XpService xpService,
        AppDbContext db)
    {
        _userManager = userManager;
        _xpService = xpService;
        _db = db;
    }

    public async Task Challenge(string opponentId, int subjectId, string subjectName, string level)
    {
        try
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

            var oldRooms = await _db.BattleRooms
                .Where(r =>
                    r.Status != "Finished" &&
                    (r.Player1Id == challenger.Id ||
                     r.Player2Id == challenger.Id ||
                     r.Player1Id == opponentId ||
                     r.Player2Id == opponentId))
                .ToListAsync();

            foreach (var old in oldRooms)
            {
                old.Status = "Finished";
                old.FinishedAt = DateTime.UtcNow;
            }

            var roomId = Guid.NewGuid().ToString("N")[..8];

            var room = new BattleRoomEntity
            {
                RoomId = roomId,
                SubjectId = subjectId,
                SubjectName = subjectName,
                Level = level,
                Player1Id = challenger.Id,
                Player1Name = string.IsNullOrWhiteSpace(challenger.FullName)
                    ? challenger.Email ?? "Player 1"
                    : challenger.FullName,
                Player2Id = opponentId,
                Player2Name = "",
                Status = "Waiting",
                CreatedAt = DateTime.UtcNow
            };

            _db.BattleRooms.Add(room);
            await _db.SaveChangesAsync();

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
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("BattleError", "Lỗi BattleHub Challenge: " + ex.Message);
            throw;
        }
    }

    public async Task AcceptChallenge(string roomId)
    {
        var room = await _db.BattleRooms
            .FirstOrDefaultAsync(r => r.RoomId == roomId && r.Status != "Finished");

        if (room == null)
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

        room.Player2Name = string.IsNullOrWhiteSpace(user.FullName)
            ? user.Email ?? "Player 2"
            : user.FullName;

        room.Status = "Ready";

        await _db.SaveChangesAsync();

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
        var room = await _db.BattleRooms
            .FirstOrDefaultAsync(r => r.RoomId == roomId && r.Status != "Finished");

        if (room == null)
        {
            await Clients.Caller.SendAsync("BattleError", "Không tìm thấy phòng Battle.");
            return;
        }

        room.Status = "Finished";
        room.FinishedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await Clients.User(room.Player1Id).SendAsync("ChallengeDeclined");
    }

    public async Task JoinRoom(string roomId)
    {
        var room = await _db.BattleRooms
            .FirstOrDefaultAsync(r => r.RoomId == roomId && r.Status != "Finished");

        if (room == null)
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

        await _db.SaveChangesAsync();

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

        var freshRoom = await _db.BattleRooms
            .FirstOrDefaultAsync(r => r.RoomId == roomId && r.Status != "Finished");

        if (freshRoom == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(freshRoom.Player1ConnectionId) &&
            !string.IsNullOrWhiteSpace(freshRoom.Player2ConnectionId) &&
            freshRoom.Status == "Ready")
        {
            freshRoom.Status = "Playing";
            freshRoom.StartTime = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            await Clients.Group(freshRoom.RoomId).SendAsync("RoomInfo", new
            {
                roomId = freshRoom.RoomId,
                subjectId = freshRoom.SubjectId,
                subjectName = freshRoom.SubjectName,
                level = freshRoom.Level,
                player1Id = freshRoom.Player1Id,
                player1Name = freshRoom.Player1Name,
                player2Id = freshRoom.Player2Id,
                player2Name = freshRoom.Player2Name,
                status = freshRoom.Status
            });

            await Clients.Group(freshRoom.RoomId).SendAsync("BothReady");
        }
    }

    public async Task SubmitAnswer(string roomId, int questionIndex, bool isCorrect)
    {
        var room = await _db.BattleRooms
            .FirstOrDefaultAsync(r => r.RoomId == roomId && r.Status == "Playing");

        if (room == null)
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

        await _db.SaveChangesAsync();

        await Clients.Group(room.RoomId).SendAsync("ScoreUpdate", new
        {
            score1 = room.Score1,
            score2 = room.Score2
        });
    }

    public async Task FinishBattle(string roomId)
    {
        var room = await _db.BattleRooms
            .FirstOrDefaultAsync(r => r.RoomId == roomId);

        if (room == null)
        {
            return;
        }

        if (room.Status == "Finished")
        {
            return;
        }

        room.Status = "Finished";
        room.FinishedAt = DateTime.UtcNow;

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

        await _db.SaveChangesAsync();

        await Clients.Group(room.RoomId).SendAsync("BattleResult", new
        {
            result,
            winnerName,
            score1 = room.Score1,
            score2 = room.Score2,
            xpWinner,
            xpLoser
        });
    }

    public async Task BroadcastQuestions(string roomId, string questionsJson)
    {
        var room = await _db.BattleRooms
            .FirstOrDefaultAsync(r => r.RoomId == roomId && r.Status == "Playing");

        if (room == null)
        {
            await Clients.Caller.SendAsync("RoomError", "Không tìm thấy phòng Battle hoặc trận chưa bắt đầu.");
            return;
        }

        await Clients.Group(room.RoomId).SendAsync("ReceiveQuestions", questionsJson);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = _userManager.GetUserId(Context.User!);

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var room = await _db.BattleRooms
                .FirstOrDefaultAsync(r =>
                    r.Status == "Playing" &&
                    (r.Player1Id == userId || r.Player2Id == userId));

            if (room != null)
            {
                room.Status = "Finished";
                room.FinishedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                await Clients.Group(room.RoomId).SendAsync("OpponentLeft");
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}