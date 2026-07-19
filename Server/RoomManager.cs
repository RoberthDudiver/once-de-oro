using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using OnceDeOro.Models;
using OnceDeOro.Multiplayer;
using OnceDeOro.Server.Hubs;
using OnceDeOro.Services;

namespace OnceDeOro.Server;

public sealed class Member
{
    public required string Id { get; init; }     // connection id
    public string Name { get; set; } = "Jugador";
    public bool Connected { get; set; } = true;
    public bool Eliminated { get; set; }
    public int SeriesWins { get; set; }            // marcador dentro de la llave actual
    public TeamDto? Team { get; set; }
    public bool HasTeam => Team is not null;
}

public sealed class Room
{
    public required string Code { get; init; }
    public required RoomConfig Config { get; init; }
    public string HostId { get; set; } = "";
    public string Phase { get; set; } = "lobby";  // lobby | building | playing | done
    public string Title { get; set; } = "";
    public string? Pair0 { get; set; }
    public string? Pair1 { get; set; }
    public string ChampionId { get; set; } = "";
    public List<Member> Members { get; } = new();
    public bool Running { get; set; }
}

/// <summary>Gestiona las salas en memoria y corre la contienda con streaming en vivo.</summary>
public sealed class RoomManager
{
    private readonly ConcurrentDictionary<string, Room> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _connToRoom = new();
    private readonly IHubContext<DuelHub> _hub;
    private readonly MatchEngine _engine = new();

    public RoomManager(IHubContext<DuelHub> hub) => _hub = hub;

    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private string NewCode()
    {
        string code;
        do
        {
            code = new string(Enumerable.Range(0, 4)
                .Select(_ => Alphabet[Random.Shared.Next(Alphabet.Length)]).ToArray());
        } while (_rooms.ContainsKey(code));
        return code;
    }

    public Room CreateRoom(RoomConfig config, string connId, string name)
    {
        config.Capacity = config.Format == MatchFormat.MiniTournament
            ? Math.Clamp(config.Capacity, 3, 4) : 2;
        var room = new Room { Code = NewCode(), Config = config, HostId = connId };
        room.Members.Add(new Member { Id = connId, Name = Clean(name) });
        _rooms[room.Code] = room;
        _connToRoom[connId] = room.Code;
        return room;
    }

    public Room? Get(string code) => _rooms.TryGetValue(code, out var r) ? r : null;
    public Room? RoomOf(string connId) => _connToRoom.TryGetValue(connId, out var c) ? Get(c) : null;

    public (bool ok, string error) Join(string code, string connId, string name)
    {
        var room = Get(code);
        if (room is null) return (false, "Sala no encontrada.");
        if (room.Phase != "lobby") return (false, "La sala ya empezó.");
        if (room.Members.Count >= room.Config.Capacity) return (false, "La sala está llena.");
        room.Members.Add(new Member { Id = connId, Name = Clean(name) });
        _connToRoom[connId] = code;
        return (true, "");
    }

    /// <summary>
    /// Registra el equipo de un jugador. Un mismo futbolista NO puede estar en dos
    /// equipos de la sala: si alguien ya lo fichó, se rechaza el envío y se avisa
    /// cuáles están ocupados (el que llega primero se lo queda).
    /// </summary>
    public (bool ok, string? error) SetTeam(string connId, TeamDto team)
    {
        var room = RoomOf(connId);
        var m = room?.Members.FirstOrDefault(x => x.Id == connId);
        if (room is null || m is null) return (false, "No estás en una sala.");

        var taken = TakenBy(room, exceptConnId: connId);
        var clash = team.Starters
                        .Where(s => !EsCantera(s) && taken.Contains(s.Name))
                        .Select(s => s.Name)
                        .Distinct()
                        .ToList();

        if (clash.Count > 0)
            return (false, $"Ya los fichó tu rival: {string.Join(", ", clash)}. Elegí otros.");

        m.Team = team;
        return (true, null);
    }

    /// <summary>
    /// Los juveniles de cantera que rellenan huecos son los mismos para todos (el
    /// Id se genera igual en cada partida), así que NO pueden considerarse fichados
    /// por nadie: si no, dos jugadores con el plantel incompleto se bloqueaban
    /// mutuamente y no había forma de destrabarlo.
    /// </summary>
    private static bool EsCantera(PlayerLite s) =>
        string.IsNullOrEmpty(s.Id) || s.Id.StartsWith("res-", StringComparison.Ordinal);

    /// <summary>
    /// Jugadores ya tomados por los demás, por NOMBRE y no por Id. El nombre es lo
    /// que identifica a la carta: "Lionel Messi" y "Lionel Messi (Prime 2012)" son
    /// dos jugadores distintos y pueden estar uno en cada equipo, mientras que dos
    /// copias del mismo prime (que tienen Ids distintos porque salieron de cajas
    /// distintas) sí se pisan.
    /// </summary>
    private static HashSet<string> TakenBy(Room room, string? exceptConnId = null) =>
        room.Members
            .Where(x => x.Id != exceptConnId && x.Team is not null)
            .SelectMany(x => x.Team!.Starters)
            .Where(s => !EsCantera(s))
            .Select(s => s.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToHashSet();

    public void Disconnect(string connId)
    {
        var room = RoomOf(connId);
        _connToRoom.TryRemove(connId, out _);
        if (room is null) return;
        var m = room.Members.FirstOrDefault(x => x.Id == connId);
        if (m is not null) m.Connected = false;
        if (room.Members.All(x => !x.Connected))
            _rooms.TryRemove(room.Code, out _);
    }

    private static string Clean(string s) =>
        string.IsNullOrWhiteSpace(s) ? "Jugador" : s.Trim()[..Math.Min(18, s.Trim().Length)];

    // ---------------------------------------------------------------- estado
    public RoomStateDto StateOf(Room room) => new()
    {
        Code = room.Code,
        HostId = room.HostId,
        Config = room.Config,
        Phase = room.Phase,
        Title = room.Title,
        Pair0 = room.Pair0,
        Pair1 = room.Pair1,
        Members = room.Members.Select(m => new RoomMemberDto
        {
            Id = m.Id,
            Name = m.Name,
            HasTeam = m.HasTeam,
            Ready = m.HasTeam,
            Connected = m.Connected,
            SeriesWins = m.SeriesWins,
            Eliminated = m.Eliminated,
        }).ToList(),
        TakenPlayers = TakenBy(room).ToList(),
    };

    public Task BroadcastState(Room room) =>
        _hub.Clients.Group(room.Code).SendAsync("RoomState", StateOf(room));

    // ---------------------------------------------------------------- flujo
    public async Task StartBuilding(Room room)
    {
        if (room.Phase != "lobby") return;
        room.Phase = "building";
        await BroadcastState(room);
    }

    /// <summary>Cuando todos enviaron equipo, corre la contienda entera.</summary>
    public async Task MaybeRun(Room room)
    {
        if (room.Running || room.Phase != "building") return;
        var active = room.Members.Where(m => m.Connected).ToList();
        if (active.Count < 2 || active.Any(m => !m.HasTeam)) return;
        room.Running = true;
        await RunEventAsync(room);
    }

    private async Task RunEventAsync(Room room)
    {
        try
        {
            int need = room.Config.Format == MatchFormat.BestOf3 ? 2 : 1;
            var alive = room.Members.Where(m => m.Connected).Select(m => m.Id).ToList();
            int round = 0;

            while (alive.Count > 1)
            {
                round++;
                var winners = new List<string>();
                var q = new Queue<string>(alive);
                while (q.Count > 0)
                {
                    var a = q.Dequeue();
                    if (q.Count == 0) { winners.Add(a); break; } // bye
                    var b = q.Dequeue();
                    var winner = await PlayTieAsync(room, a, b, need, TieTitle(room, alive.Count, round));
                    winners.Add(winner);
                    var loser = winner == a ? b : a;
                    var lm = room.Members.First(m => m.Id == loser);
                    lm.Eliminated = true;
                    await BroadcastState(room);
                    await Task.Delay(1500);
                }
                alive = winners;
            }

            room.ChampionId = alive.FirstOrDefault() ?? "";
            room.Phase = "done";
            room.Pair0 = room.Pair1 = null;
            await BroadcastState(room);
        }
        catch { /* si algo falla, cerramos la contienda */ room.Phase = "done"; await BroadcastState(room); }
        finally { room.Running = false; }
    }

    private async Task<string> PlayTieAsync(Room room, string a, string b, int need, string title)
    {
        var ma = room.Members.First(m => m.Id == a);
        var mb = room.Members.First(m => m.Id == b);
        ma.SeriesWins = 0; mb.SeriesWins = 0;
        room.Pair0 = a; room.Pair1 = b; room.Phase = "playing"; room.Title = title;

        while (ma.SeriesWins < need && mb.SeriesWins < need)
        {
            var (ap, ast) = Build(ma.Team!);
            var (bp, bst) = Build(mb.Team!);
            var powA = MatchEngine.PowerOf(ap, ast);
            var powB = MatchEngine.PowerOf(bp, bst);

            await _hub.Clients.Group(room.Code).SendAsync("MatchStart", new MatchStartDto
            {
                HomeId = a, AwayId = b, HomeName = ma.Name, AwayName = mb.Name,
                HomeFlag = ModalFlag(ma.Team!), AwayFlag = ModalFlag(mb.Team!),
                HomePower = powA.Overall, AwayPower = powB.Overall,
                Title = title, HomeSeriesWins = ma.SeriesWins, AwaySeriesWins = mb.SeriesWins,
            });
            await Task.Delay(1600);

            var r = _engine.SimulatePvP(ma.Name, ModalFlag(ma.Team!), powA, ap,
                                        mb.Name, ModalFlag(mb.Team!), powB, bp, allowPenalties: true);

            for (int minute = 1; minute <= 90; minute++)
            {
                var goals = r.Goals.Where(g => g.Minute == minute).ToList();
                if (goals.Count == 0)
                    await _hub.Clients.Group(room.Code).SendAsync("Tick", minute, (GoalDto?)null);
                else
                    foreach (var g in goals)
                        await _hub.Clients.Group(room.Code).SendAsync("Tick", minute,
                            new GoalDto { Minute = minute, HomeSide = g.HomeSide, Scorer = g.Scorer });
                await Task.Delay(goals.Count > 0 ? 650 : 32);
            }

            bool aWon = r.HomeWon;
            if (aWon) ma.SeriesWins++; else mb.SeriesWins++;

            await _hub.Clients.Group(room.Code).SendAsync("MatchEnd", new MatchResultDto
            {
                HomeName = ma.Name, AwayName = mb.Name,
                HomeFlag = ModalFlag(ma.Team!), AwayFlag = ModalFlag(mb.Team!),
                HomeGoals = r.HomeGoals, AwayGoals = r.AwayGoals,
                Penalties = r.WentToPenalties, HomePens = r.HomePens, AwayPens = r.AwayPens,
                WinnerId = aWon ? a : b,
                Goals = r.Goals.Select(g => new GoalDto { Minute = g.Minute, HomeSide = g.HomeSide, Scorer = g.Scorer }).ToList(),
            });
            await BroadcastState(room);
            await Task.Delay(need > 1 ? 3200 : 2600);
        }

        return ma.SeriesWins > mb.SeriesWins ? a : b;
    }

    private static string TieTitle(Room room, int aliveCount, int round)
    {
        if (room.Config.Format == MatchFormat.BestOf3) return "Mejor de 3";
        if (room.Config.Format == MatchFormat.Single) return "Duelo único";
        return aliveCount switch { 2 => "Final", 3 or 4 => "Semifinal", _ => $"Ronda {round}" };
    }

    private static (List<Player>, TeamStyle) Build(TeamDto t)
    {
        var players = t.Starters.Select(s => new Player
        {
            Id = s.Id, Name = s.Name, Nation = "", Flag = s.Flag,
            Pos = Enum.TryParse<Position>(s.Pos, out var p) ? p : Position.MID,
            Rating = s.Rating,
        }).ToList();
        var style = Enum.TryParse<TeamStyle>(t.Style, out var st) ? st : TeamStyle.Equilibrado;
        return (players, style);
    }

    private static string ModalFlag(TeamDto t)
    {
        var g = t.Starters.Where(s => s.Flag != "🎓" && s.Flag != "🌐")
                          .GroupBy(s => s.Flag).OrderByDescending(x => x.Count()).FirstOrDefault();
        return g?.Key ?? "⚽";
    }
}
