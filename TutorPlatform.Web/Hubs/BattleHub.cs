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
    private readonly ILogger<BattleHub> _logger;

    public BattleHub(
        UserManager<AppUser> userManager,
        XpService xpService,
        AppDbContext db,
        ILogger<BattleHub> logger)
    {
        _userManager = userManager;
        _xpService = xpService;
        _db = db;
        _logger = logger;
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

            var opponent = await _userManager.FindByIdAsync(opponentId);

            if (opponent == null)
            {
                await Clients.Caller.SendAsync("BattleError", "Tài khoản đối thủ không tồn tại.");
                return;
            }

            // FIX: Chỉ xóa room "Waiting" của chính challenger (không xóa room của opponent)
            // Tránh xóa room đang "Playing" hoặc "Ready" của người khác
            var oldRooms = await _db.BattleRooms
                .Where(r =>
                    r.Status == "Waiting" &&
                    (r.Player1Id == challenger.Id || r.Player2Id == challenger.Id))
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
                SubjectName = string.IsNullOrWhiteSpace(subjectName) ? "Không rõ môn học" : subjectName,
                Level = string.IsNullOrWhiteSpace(level) ? "Trung bình" : level,

                Player1Id = challenger.Id,
                Player1Name = string.IsNullOrWhiteSpace(challenger.FullName)
                    ? challenger.Email ?? "Player 1"
                    : challenger.FullName,

                Player2Id = opponentId,
                Player2Name = string.IsNullOrWhiteSpace(opponent.FullName)
                    ? opponent.Email ?? "Player 2"
                    : opponent.FullName,

                Player1ConnectionId = null,
                Player2ConnectionId = null,

                Score1 = 0,
                Score2 = 0,

                Status = "Waiting",
                CreatedAt = DateTime.UtcNow,
                StartTime = null,
                FinishedAt = null
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
            var realError = ex.GetBaseException().Message;
            _logger.LogError(ex, "Battle Challenge error");
            await Clients.Caller.SendAsync("BattleError", "Lỗi server khi gửi thách đấu: " + realError);
        }
    }

    public async Task AcceptChallenge(string roomId)
    {
        try
        {
            // FIX: Chấp nhận cả room "Waiting" (trạng thái ban đầu khi mới tạo)
            var room = await _db.BattleRooms
                .FirstOrDefaultAsync(r => r.RoomId == roomId && (r.Status == "Waiting" || r.Status == "Ready"));

            if (room == null)
            {
                await Clients.Caller.SendAsync("BattleError", "Không tìm thấy phòng Battle hoặc phòng đã hết hạn.");
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

            // Gửi cho Player1 (challenger)
            await Clients.User(room.Player1Id).SendAsync("ChallengeAccepted", new
            {
                roomId = room.RoomId
            });

            // Gửi cho Player2 (người accept)
            await Clients.Caller.SendAsync("ChallengeAccepted", new
            {
                roomId = room.RoomId
            });
        }
        catch (Exception ex)
        {
            var realError = ex.GetBaseException().Message;
            _logger.LogError(ex, "AcceptChallenge error");
            await Clients.Caller.SendAsync("BattleError", "Lỗi server khi chấp nhận Battle: " + realError);
        }
    }

    public async Task DeclineChallenge(string roomId)
    {
        try
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
        catch (Exception ex)
        {
            var realError = ex.GetBaseException().Message;
            _logger.LogError(ex, "DeclineChallenge error");
            await Clients.Caller.SendAsync("BattleError", "Lỗi server khi từ chối Battle: " + realError);
        }
    }

    public async Task JoinRoom(string roomId)
    {
        try
        {
            // FIX: Tìm room không phân biệt status (trừ Finished)
            // Cho phép join room ở bất kỳ trạng thái "Waiting", "Ready", "Playing"
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

            // Re-query để lấy trạng thái mới nhất sau khi cả 2 đã cập nhật ConnectionId
            var freshRoom = await _db.BattleRooms
                .FirstOrDefaultAsync(r => r.RoomId == roomId && r.Status != "Finished");

            if (freshRoom == null)
            {
                return;
            }

            bool bothConnected = !string.IsNullOrWhiteSpace(freshRoom.Player1ConnectionId) &&
                                 !string.IsNullOrWhiteSpace(freshRoom.Player2ConnectionId);

            // FIX: Cho phép chuyển từ "Waiting" hoặc "Ready" sang "Playing"
            // (trước đây chỉ check Status == "Ready", bỏ lỡ case "Waiting")
            bool canStart = freshRoom.Status == "Ready" || freshRoom.Status == "Waiting";

            if (bothConnected && canStart)
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
        catch (Exception ex)
        {
            var realError = ex.GetBaseException().Message;
            _logger.LogError(ex, "JoinRoom error");
            await Clients.Caller.SendAsync("RoomError", "Lỗi server khi vào phòng Battle: " + realError);
        }
    }

    public async Task SubmitAnswer(string roomId, int questionIndex, bool isCorrect)
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "SubmitAnswer error");
        }
    }

    public async Task FinishBattle(string roomId)
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "FinishBattle error");
        }
    }

    public async Task BroadcastQuestions(string roomId, string questionsJson)
    {
        try
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
        catch (Exception ex)
        {
            var realError = ex.GetBaseException().Message;
            _logger.LogError(ex, "BroadcastQuestions error");
            await Clients.Caller.SendAsync("RoomError", "Lỗi gửi câu hỏi Battle: " + realError);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OnDisconnectedAsync error");
        }

        await base.OnDisconnectedAsync(exception);
    }
}