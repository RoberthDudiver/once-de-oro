using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using OnceDeOro.Multiplayer;

namespace OnceDeOro.Services;

/// <summary>Cliente SignalR del modo online: salas, envío de equipo y partido en vivo.</summary>
public sealed class MultiplayerService : IAsyncDisposable
{
    private readonly NavigationManager _nav;
    private HubConnection? _hub;

    public MultiplayerService(NavigationManager nav) => _nav = nav;

    public event Action? Changed;
    private void Notify() => Changed?.Invoke();

    public string? MyId { get; private set; }
    public string? Error { get; private set; }
    public RoomStateDto? Room { get; private set; }
    public bool Connected => _hub?.State == HubConnectionState.Connected;

    // Estado del partido en vivo (lo maneja el servidor por streaming)
    public MatchStartDto? CurrentMatch { get; private set; }
    public MatchResultDto? LastResult { get; private set; }
    public int Minute { get; private set; }
    public int HomeShown { get; private set; }
    public int AwayShown { get; private set; }
    public int FlashId { get; private set; }
    public string FlashText { get; private set; } = "";
    public List<GoalDto> Feed { get; } = new();
    public bool ShowResult { get; private set; }

    public bool IsHost => Room is not null && Room.HostId == MyId;
    public RoomMemberDto? Me => Room?.Members.FirstOrDefault(m => m.Id == MyId);

    public async Task EnsureConnectedAsync()
    {
        if (_hub is not null && _hub.State == HubConnectionState.Connected) return;

        _hub = new HubConnectionBuilder()
            .WithUrl(_nav.ToAbsoluteUri("duelhub"))
            .WithAutomaticReconnect()
            .Build();

        _hub.On<RoomStateDto>("RoomState", state =>
        {
            Room = state;
            Notify();
        });

        _hub.On<MatchStartDto>("MatchStart", m =>
        {
            CurrentMatch = m;
            LastResult = null;
            ShowResult = false;
            Minute = 0; HomeShown = 0; AwayShown = 0; FlashId = 0;
            Feed.Clear();
            Notify();
        });

        _hub.On<int, GoalDto?>("Tick", (minute, goal) =>
        {
            Minute = minute;
            if (goal is not null)
            {
                if (goal.HomeSide) HomeShown++; else AwayShown++;
                Feed.Insert(0, goal);
                FlashId++;
                FlashText = GoalIsMine(goal) ? "¡GOOOL!" : "GOL RIVAL";
            }
            Notify();
        });

        _hub.On<MatchResultDto>("MatchEnd", r =>
        {
            LastResult = r;
            HomeShown = r.HomeGoals; AwayShown = r.AwayGoals;
            ShowResult = true;
            Notify();
        });

        _hub.On<string>("Error", err => { Error = err; Notify(); });

        await _hub.StartAsync();
        MyId = _hub.ConnectionId;
        Notify();
    }

    private bool GoalIsMine(GoalDto g)
    {
        if (CurrentMatch is null) return false;
        bool iAmHome = CurrentMatch.HomeId == MyId;
        return g.HomeSide == iAmHome;
    }

    public async Task<string> CreateRoomAsync(RoomConfig config, string name)
    {
        await EnsureConnectedAsync();
        Error = null;
        return await _hub!.InvokeAsync<string>("CreateRoom", config, name);
    }

    public async Task<string> JoinRoomAsync(string code, string name)
    {
        await EnsureConnectedAsync();
        Error = null;
        return await _hub!.InvokeAsync<string>("JoinRoom", code.Trim().ToUpperInvariant(), name);
    }

    public Task StartEventAsync() => _hub!.InvokeAsync("StartEvent");
    public Task SubmitTeamAsync(TeamDto team) => _hub!.InvokeAsync("SubmitTeam", team);

    public void ClearError() { Error = null; Notify(); }

    /// <summary>Sale de la sala y limpia el estado local (no cierra la conexión).</summary>
    public void Reset()
    {
        Room = null;
        CurrentMatch = null;
        LastResult = null;
        ShowResult = false;
        Minute = HomeShown = AwayShown = FlashId = 0;
        Feed.Clear();
        Notify();
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null) await _hub.DisposeAsync();
    }
}
