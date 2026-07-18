using Microsoft.AspNetCore.SignalR;
using OnceDeOro.Multiplayer;

namespace OnceDeOro.Server.Hubs;

/// <summary>Hub de duelos online: salas, envío de equipos y contienda en vivo.</summary>
public sealed class DuelHub : Hub
{
    private readonly RoomManager _rooms;
    public DuelHub(RoomManager rooms) => _rooms = rooms;

    public async Task<string> CreateRoom(RoomConfig config, string name)
    {
        var room = _rooms.CreateRoom(config, Context.ConnectionId, name);
        await Groups.AddToGroupAsync(Context.ConnectionId, room.Code);
        await _rooms.BroadcastState(room);
        return room.Code;
    }

    public async Task<string> JoinRoom(string code, string name)
    {
        var (ok, error) = _rooms.Join(code, Context.ConnectionId, name);
        if (!ok)
        {
            await Clients.Caller.SendAsync("Error", error);
            return "";
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, code.ToUpperInvariant());
        var room = _rooms.Get(code)!;
        await _rooms.BroadcastState(room);
        return room.Code;
    }

    public async Task StartEvent()
    {
        var room = _rooms.RoomOf(Context.ConnectionId);
        if (room is null || room.HostId != Context.ConnectionId) return;
        if (room.Members.Count(m => m.Connected) < 2)
        {
            await Clients.Caller.SendAsync("Error", "Necesitás al menos 2 jugadores.");
            return;
        }
        await _rooms.StartBuilding(room);
    }

    public async Task SubmitTeam(TeamDto team)
    {
        _rooms.SetTeam(Context.ConnectionId, team);
        var room = _rooms.RoomOf(Context.ConnectionId);
        if (room is null) return;
        await _rooms.BroadcastState(room);
        await _rooms.MaybeRun(room);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var room = _rooms.RoomOf(Context.ConnectionId);
        _rooms.Disconnect(Context.ConnectionId);
        if (room is not null) await _rooms.BroadcastState(room);
        await base.OnDisconnectedAsync(exception);
    }
}
