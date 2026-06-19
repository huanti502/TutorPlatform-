using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace TutorPlatform.Web.Hubs;

[Authorize]
public class BattleHub : Hub
{
    private readonly UserManager<AppUser> _userManager;
    private readonly XpService _xpService;
    private readonly AppDbContext _db;
    private readonly ILogger<BattleHub> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<BattleHub> _hubContext;

    // Thời gian ân hạn cho phép người chơi kết nối lại trước khi tính là "rời trận".
    private const int DisconnectGraceSeconds = 12;

    // ─── Lưu mapping: userId → connectionId (in-memory, đủ dùng cho 1 instance)
    // Key = userId, Value = connectionId hiện tại
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string>
        _userConnections = new();

    // ─── Lưu câu hỏi của mỗi phòng (in-memory) để khi người chơi F5 / reconnect
    // còn gửi lại được. Key = roomId, Value = JSON câu hỏi.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string>
        _roomQuestions = new();

    // ─── Lưu các câu mỗi người đã trả lời, chống cộng điểm trùng khi F5 trả lời lại.
    // Key = "roomId:userId", Value = tập chỉ số câu đã trả lời.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, HashSet<int>>
        _answeredQuestions = new();

    // ─── Theo dõi ai đã THỰC SỰ vào phòng (gọi JoinRoom). Dùng để xác định
    // "cả 2 đã sẵn sàng" thay vì dựa vào ConnectionId trong DB (có thể là
    // connection cũ từ trang /Battle gây hiểu nhầm). Key = roomId, Value = tập userId.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, HashSet<string>>
        _roomPresence = new();

    private const int BattleDurationSeconds = 60;

    public BattleHub(
        UserManager<AppUser> userManager,
        XpService xpService,
        AppDbContext db,
        ILogger<BattleHub> logger,
        IServiceScopeFactory scopeFactory,
        IHubContext<BattleHub> hubContext)
    {
        _userManager = userManager;
        _xpService = xpService;
        _db = db;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
    }

    // ─── Ghi nhận connection khi user kết nối
    public override async Task OnConnectedAsync()
    {
        var userId = _userManager.GetUserId(Context.User!);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            _userConnections[userId] = Context.ConnectionId;
        }
        await base.OnConnectedAsync();
    }

    // ─── Xóa connection khi user ngắt kết nối
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var userId = _userManager.GetUserId(Context.User!);

            if (!string.IsNullOrWhiteSpace(userId))
            {
                // Chỉ xóa mapping nếu connectionId đang lưu đúng là cái vừa ngắt.
                // Tránh xóa nhầm connection mới hơn (khi user mở nhiều tab / vừa chuyển trang).
                if (_userConnections.TryGetValue(userId, out var storedConn) &&
                    storedConn == Context.ConnectionId)
                {
                    _userConnections.TryRemove(userId, out _);
                }

                // FIX RACE CONDITION + RECONNECT:
                // Không kết thúc trận NGAY khi connection rớt. Một cú rớt mạng tạm thời
                // (chuyển tab, sleep, mạng chập chờn, hoặc connection cũ từ trang /Battle
                // ngắt muộn) không được giết trận đang chơi — nếu không cả 2 sẽ bị
                // "OpponentLeft" rồi khi tự reconnect lại báo "Không tìm thấy phòng".
                //
                // Thay vào đó: chỉ đánh dấu ứng viên rời trận, chờ DisconnectGraceSeconds.
                // Nếu trong thời gian đó người chơi kết nối lại (JoinRoom sẽ ghi đè
                // ConnectionId bằng một id MỚI), ta phát hiện id đã đổi → bỏ qua, trận
                // tiếp tục. Chỉ khi họ thật sự không quay lại mới kết thúc + OpponentLeft.
                var room = await _db.BattleRooms
                    .FirstOrDefaultAsync(r =>
                        r.Status == "Playing" &&
                        ((r.Player1Id == userId && r.Player1ConnectionId == Context.ConnectionId) ||
                         (r.Player2Id == userId && r.Player2ConnectionId == Context.ConnectionId)));

                if (room != null)
                {
                    ScheduleLeaveCheck(room.RoomId, userId, Context.ConnectionId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OnDisconnectedAsync error");
        }

        await base.OnDisconnectedAsync(exception);
    }

    // ─── Lên lịch kiểm tra "rời trận" sau thời gian ân hạn.
    // Hub là transient và _db sẽ bị dispose sau khi OnDisconnectedAsync trả về,
    // nên tác vụ trễ phải mở một DI scope MỚI và gửi qua IHubContext.
    private void ScheduleLeaveCheck(string roomId, string userId, string disconnectedConnectionId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(DisconnectGraceSeconds));

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var room = await db.BattleRooms.FirstOrDefaultAsync(r => r.RoomId == roomId);

                // Trận đã kết thúc bình thường rồi → không cần làm gì.
                if (room == null || room.Status == "Finished") return;

                // Người chơi này đã KẾT NỐI LẠI nếu ConnectionId hiện tại trong phòng
                // KHÁC với connection đã rớt. Khi đó: bỏ qua, trận vẫn tiếp tục.
                bool stillGone =
                    (room.Player1Id == userId && room.Player1ConnectionId == disconnectedConnectionId) ||
                    (room.Player2Id == userId && room.Player2ConnectionId == disconnectedConnectionId);

                if (!stillGone) return;

                // Hết ân hạn mà vẫn không quay lại → kết thúc trận, báo đối thủ.
                room.Status = "Finished";
                room.FinishedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();

                _roomPresence.TryRemove(roomId, out _);
                await _hubContext.Clients.Group(roomId).SendAsync("OpponentLeft");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ScheduleLeaveCheck error for room {RoomId}", roomId);
            }
        });
    }

    // ─── Helper: gửi message đến user qua ConnectionId (đáng tin hơn Clients.User)
    private async Task SendToUser(string userId, string method, object arg)
    {
        // Thử gửi qua ConnectionId đã lưu
        if (_userConnections.TryGetValue(userId, out var connId))
        {
            try
            {
                await Clients.Client(connId).SendAsync(method, arg);
                return;
            }
            catch
            {
                // Connection có thể đã hết hạn, fallback xuống Clients.User
                _userConnections.TryRemove(userId, out _);
            }
        }

        // Fallback: dùng Clients.User (Identity claim)
        await Clients.User(userId).SendAsync(method, arg);
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

            // Chỉ xóa room "Waiting" của chính challenger
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

                // Lưu ConnectionId của challenger ngay bây giờ
                Player1ConnectionId = Context.ConnectionId,

                Player2Id = opponentId,
                Player2Name = string.IsNullOrWhiteSpace(opponent.FullName)
                    ? opponent.Email ?? "Player 2"
                    : opponent.FullName,

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

            // Ghi nhận connection của challenger cho room này
            _userConnections[challenger.Id] = Context.ConnectionId;

            await SendToUser(opponentId, "ReceiveBattleInvite", new
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
            _logger.LogError(ex, "Battle Challenge error");
            await Clients.Caller.SendAsync("BattleError", "Lỗi server khi gửi thách đấu: " + ex.GetBaseException().Message);
        }
    }

    public async Task AcceptChallenge(string roomId)
    {
        try
        {
            // Chấp nhận cả "Waiting" và "Ready"
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

            var acceptedPayload = new { roomId = room.RoomId };

            // Gửi cho Player1 (challenger) — dùng ConnectionId đã lưu (đáng tin hơn)
            if (!string.IsNullOrWhiteSpace(room.Player1ConnectionId))
            {
                try
                {
                    await Clients.Client(room.Player1ConnectionId).SendAsync("ChallengeAccepted", acceptedPayload);
                }
                catch
                {
                    // ConnectionId cũ, thử qua _userConnections hoặc Clients.User
                    await SendToUser(room.Player1Id, "ChallengeAccepted", acceptedPayload);
                }
            }
            else
            {
                await SendToUser(room.Player1Id, "ChallengeAccepted", acceptedPayload);
            }

            // Gửi cho Player2 (người accept)
            await Clients.Caller.SendAsync("ChallengeAccepted", acceptedPayload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AcceptChallenge error");
            await Clients.Caller.SendAsync("BattleError", "Lỗi server khi chấp nhận Battle: " + ex.GetBaseException().Message);
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

            await SendToUser(room.Player1Id, "ChallengeDeclined", new { });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeclineChallenge error");
            await Clients.Caller.SendAsync("BattleError", "Lỗi server khi từ chối Battle: " + ex.GetBaseException().Message);
        }
    }

    public async Task JoinRoom(string roomId)
    {
        try
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

            // Vào group SignalR TRƯỚC khi báo "cả 2 sẵn sàng" để chắc chắn nhận BothReady.
            await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomId);

            // Cập nhật ConnectionId hiện tại (mới nhất) — phục vụ logic reconnect/disconnect.
            _userConnections[userId] = Context.ConnectionId;
            if (userId == room.Player1Id) room.Player1ConnectionId = Context.ConnectionId;
            else room.Player2ConnectionId = Context.ConnectionId;
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

            // ─── Ghi nhận người này đã vào phòng. Chỉ khi CẢ Player1 và Player2 đều
            // đã thực sự gọi JoinRoom (đều ở trong group) mới bắt đầu trận.
            var presence = _roomPresence.GetOrAdd(roomId, _ => new HashSet<string>());
            bool bothPresent;
            lock (presence)
            {
                presence.Add(userId);
                bothPresent = presence.Contains(room.Player1Id) && presence.Contains(room.Player2Id);
            }

            if (bothPresent)
            {
                // Chuyển "Ready"/"Waiting" → "Playing" NGUYÊN TỬ ở mức DB: chỉ MỘT lời gọi
                // đồng thời thắng (changed > 0) nên BothReady chỉ phát đúng một lần.
                int changed = await _db.BattleRooms
                    .Where(r => r.RoomId == roomId && (r.Status == "Ready" || r.Status == "Waiting"))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(r => r.Status, "Playing")
                        .SetProperty(r => r.StartTime, DateTime.UtcNow));

                if (changed > 0)
                {
                    var roomInfoPayload = new
                    {
                        roomId = room.RoomId,
                        subjectId = room.SubjectId,
                        subjectName = room.SubjectName,
                        level = room.Level,
                        player1Id = room.Player1Id,
                        player1Name = room.Player1Name,
                        player2Id = room.Player2Id,
                        player2Name = room.Player2Name,
                        status = "Playing"
                    };

                    await Clients.Group(room.RoomId).SendAsync("RoomInfo", roomInfoPayload);
                    await Clients.Group(room.RoomId).SendAsync("BothReady");
                    return;
                }
            }

            // ─── Không phải lần khởi động: hoặc đang chờ người kia, hoặc trận đã "Playing".
            // Đọc lại trạng thái thật từ DB (tránh dữ liệu cache của EF), nếu trận đã bắt
            // đầu và đã có câu hỏi thì đây là REJOIN (F5/reconnect) → gửi lại để chơi tiếp.
            await _db.Entry(room).ReloadAsync();

            if (room.Status == "Playing" && _roomQuestions.TryGetValue(roomId, out var savedQuestions))
            {
                int secondsLeft = room.StartTime.HasValue
                    ? Math.Max(0, BattleDurationSeconds - (int)(DateTime.UtcNow - room.StartTime.Value).TotalSeconds)
                    : BattleDurationSeconds;

                int answeredCount = _answeredQuestions.TryGetValue($"{roomId}:{userId}", out var done)
                    ? done.Count
                    : 0;

                await Clients.Caller.SendAsync("ResumeBattle", new
                {
                    questionsJson = savedQuestions,
                    score1 = room.Score1,
                    score2 = room.Score2,
                    secondsLeft,
                    answeredCount
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JoinRoom error");
            await Clients.Caller.SendAsync("RoomError", "Lỗi server khi vào phòng Battle: " + ex.GetBaseException().Message);
        }
    }

    public async Task SubmitAnswer(string roomId, int questionIndex, bool isCorrect)
    {
        try
        {
            var room = await _db.BattleRooms
                .FirstOrDefaultAsync(r => r.RoomId == roomId && r.Status == "Playing");

            if (room == null) return;

            var userId = _userManager.GetUserId(Context.User!);

            // Chống cộng điểm trùng: nếu người chơi F5 và trả lời lại câu đã trả lời,
            // bỏ qua lần thứ hai.
            var answeredKey = $"{roomId}:{userId}";
            var answeredSet = _answeredQuestions.GetOrAdd(answeredKey, _ => new HashSet<int>());
            lock (answeredSet)
            {
                if (!answeredSet.Add(questionIndex))
                    return; // câu này đã được tính rồi
            }

            if (userId == room.Player1Id && isCorrect) room.Score1++;
            else if (userId == room.Player2Id && isCorrect) room.Score2++;

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
            var room = await _db.BattleRooms.FirstOrDefaultAsync(r => r.RoomId == roomId);
            if (room == null || room.Status == "Finished") return;

            room.Status = "Finished";
            room.FinishedAt = DateTime.UtcNow;

            string result;
            string winnerId = "", loserId = "", winnerName = "";

            if (room.Score1 > room.Score2)
            {
                result = "player1"; winnerId = room.Player1Id; loserId = room.Player2Id; winnerName = room.Player1Name;
            }
            else if (room.Score2 > room.Score1)
            {
                result = "player2"; winnerId = room.Player2Id; loserId = room.Player1Id; winnerName = room.Player2Name;
            }
            else
            {
                result = "draw";
            }

            int xpWinner = 0, xpLoser = 0;

            if (result != "draw")
            {
                xpWinner = await _xpService.AwardXpAsync(winnerId, "quiz_pass");
                xpLoser = 10;
                var loser = await _userManager.FindByIdAsync(loserId);
                if (loser != null) { loser.XpPoints += xpLoser; await _userManager.UpdateAsync(loser); }
            }

            await _db.SaveChangesAsync();

            await Clients.Group(room.RoomId).SendAsync("BattleResult", new
            {
                result,
                winnerId,
                winnerName,
                score1 = room.Score1,
                score2 = room.Score2,
                xpWinner,
                xpLoser
            });

            // Dọn state in-memory của phòng để tránh rò rỉ bộ nhớ.
            _roomQuestions.TryRemove(room.RoomId, out _);
            _answeredQuestions.TryRemove($"{room.RoomId}:{room.Player1Id}", out _);
            _answeredQuestions.TryRemove($"{room.RoomId}:{room.Player2Id}", out _);
            _roomPresence.TryRemove(room.RoomId, out _);
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

            // Lưu lại câu hỏi để phục vụ khôi phục khi F5 / reconnect.
            _roomQuestions[room.RoomId] = questionsJson;

            await Clients.Group(room.RoomId).SendAsync("ReceiveQuestions", questionsJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BroadcastQuestions error");
            await Clients.Caller.SendAsync("RoomError", "Lỗi gửi câu hỏi Battle: " + ex.GetBaseException().Message);
        }
    }
}