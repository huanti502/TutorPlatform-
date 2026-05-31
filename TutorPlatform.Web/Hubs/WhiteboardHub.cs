using Microsoft.AspNetCore.SignalR;

namespace TutorPlatform.Web.Hubs;

public class WhiteboardHub : Hub
{
    // Track số người trong mỗi room: roomId -> count
    private static readonly Dictionary<string, HashSet<string>> _rooms = new();
    private static readonly Dictionary<string, string> _connRoom = new(); // connId -> roomId
    private static readonly object _lock = new();

    public async Task JoinRoom(string roomId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        int count;
        lock (_lock)
        {
            if (!_rooms.ContainsKey(roomId)) _rooms[roomId] = new();
            _rooms[roomId].Add(Context.ConnectionId);
            _connRoom[Context.ConnectionId] = roomId;
            count = _rooms[roomId].Count;
        }

        // Notify người đã ở trong phòng rằng có người mới vào
        await Clients.OthersInGroup(roomId).SendAsync("UserJoined", count);
        // Trả về số người hiện tại cho người vừa vào
        await Clients.Caller.SendAsync("RoomInfo", count);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string? roomId;
        int count = 0;
        lock (_lock)
        {
            _connRoom.TryGetValue(Context.ConnectionId, out roomId);
            if (roomId != null && _rooms.ContainsKey(roomId))
            {
                _rooms[roomId].Remove(Context.ConnectionId);
                count = _rooms[roomId].Count;
                if (count == 0) _rooms.Remove(roomId);
            }
            _connRoom.Remove(Context.ConnectionId);
        }

        if (roomId != null)
            await Clients.Group(roomId).SendAsync("UserLeft", count);

        await base.OnDisconnectedAsync(exception);
    }

    // Gửi nét vẽ realtime
    public async Task SendStroke(string roomId, StrokeData stroke)
    {
        await Clients.OthersInGroup(roomId).SendAsync("ReceiveStroke", stroke);
    }

    // Chat riêng (không lẫn với stroke)
    public async Task SendChat(string roomId, string senderName, string message)
    {
        await Clients.OthersInGroup(roomId).SendAsync("ReceiveChat", senderName, message);
    }

    // Xóa bảng
    public async Task ClearBoard(string roomId)
    {
        await Clients.Group(roomId).SendAsync("BoardCleared");
    }

    // Undo — gửi full canvas snapshot đến người kia
    public async Task SyncCanvas(string roomId, string dataUrl)
    {
        await Clients.OthersInGroup(roomId).SendAsync("CanvasSynced", dataUrl);
    }

    // Undo notify
    public async Task UndoStroke(string roomId)
    {
        await Clients.OthersInGroup(roomId).SendAsync("RemoteUndo");
    }
}

public class StrokeData
{
    public string Tool { get; set; } = "pen";
    public string Color { get; set; } = "#000000";
    public int LineWidth { get; set; } = 3;
    public bool IsComplete { get; set; } = false;
    public List<PointData> Points { get; set; } = new();
    public string? Text { get; set; }
    public int FontSize { get; set; } = 18;
}

public class PointData
{
    public double X { get; set; }
    public double Y { get; set; }
}
