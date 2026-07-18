using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;
using OnceDeOro.Data;
using OnceDeOro.Models;

namespace OnceDeOro.Services;

/// <summary>
/// Estado central del juego: plantel, dinero, mercado, economía y progresión de torneos.
/// Se persiste en localStorage del navegador.
/// </summary>
public sealed class GameService
{
    private const string SaveKey = "once-de-oro:save:v1";
    private readonly IJSRuntime _js;
    private readonly MatchEngine _engine;
    private readonly Random _rng = new();

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false,
    };

    public GameState State { get; private set; } = new();
    public event Action? Changed;

    /// <summary>Resultado del último partido jugado (para la pantalla de partido).</summary>
    public MatchResult? LastMatch { get; private set; }

    public GameService(IJSRuntime js, MatchEngine engine)
    {
        _js = js;
        _engine = engine;
    }

    // ---------------------------------------------------------------- persistencia
    public async Task LoadAsync()
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", SaveKey);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var loaded = JsonSerializer.Deserialize<GameState>(json, JsonOpts);
                if (loaded is not null) State = loaded;
            }
        }
        catch { /* arranque limpio si el save está corrupto */ }
        Changed?.Invoke();
    }

    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(State, JsonOpts);
            await _js.InvokeVoidAsync("localStorage.setItem", SaveKey, json);
        }
        catch { }
    }

    public async Task ResetAsync()
    {
        State = new GameState();
        await _js.InvokeVoidAsync("localStorage.removeItem", SaveKey);
        Changed?.Invoke();
    }

    private async void Commit()
    {
        Changed?.Invoke();
        await SaveAsync();
    }

    // ---------------------------------------------------------------- plantel
    public IEnumerable<Player> Owned =>
        State.OwnedIds.Select(id => PlayerDatabase.All.FirstOrDefault(p => p.Id == id))
                      .Where(p => p is not null)!.Cast<Player>();

    public IReadOnlyList<Player> Starters =>
        State.StartingIds.Select(id => PlayerDatabase.All.FirstOrDefault(p => p.Id == id))
                         .Where(p => p is not null).Cast<Player>().ToList();

    public Formation Formation =>
        Formation.All.FirstOrDefault(f => f.Name == State.FormationName) ?? Formation.F433;

    // ---- Cantera: rellena los huecos del XI para que SIEMPRE se pueda jugar ----
    private static readonly string[] ReserveNames =
        { "Cantera Pérez", "Cantera Gómez", "Cantera Ruiz", "Cantera Silva", "Cantera Mora",
          "Cantera Vega", "Cantera Ríos", "Cantera Sosa", "Cantera Luna", "Cantera Peña" };

    public static Player ReserveFor(Position pos, int idx)
    {
        int r = pos switch { Position.GK => 55, Position.DEF => 53, Position.MID => 54, Position.FWD => 52, _ => 52 };
        string name = ReserveNames[((int)pos * 3 + idx) % ReserveNames.Length];
        return new Player { Id = $"res-{pos}-{idx}", Name = name, Nation = "Cantera", Flag = "🎓", Pos = pos, Rating = r, Era = 2026 };
    }

    public bool IsReserve(Player p) => p.Id.StartsWith("res-");

    /// <summary>El XI real, con los puestos vacíos completados por juveniles de cantera.</summary>
    public IReadOnlyList<Player> EffectiveStarters
    {
        get
        {
            var f = Formation;
            var result = new List<Player>();
            foreach (var pos in new[] { Position.GK, Position.DEF, Position.MID, Position.FWD })
            {
                var reals = Starters.Where(p => p.Pos == pos).Take(f.SlotsFor(pos)).ToList();
                result.AddRange(reals);
                for (int i = reals.Count; i < f.SlotsFor(pos); i++)
                    result.Add(ReserveFor(pos, i));
            }
            return result;
        }
    }

    public TeamPower Power => MatchEngine.PowerOf(EffectiveStarters, State.Style);

    /// <summary>Siempre se puede jugar: los huecos se cubren con cantera.</summary>
    public bool CanPlay => true;

    /// <summary>Cuántos titulares son juveniles de cantera (huecos sin fichar).</summary>
    public int ReserveCount => EffectiveStarters.Count(IsReserve);

    /// <summary>true si los 11 titulares son jugadores reales (sin cantera).</summary>
    public bool HasFullRealXI => ReserveCount == 0;

    // ---------------------------------------------------------------- mercado
    public int BuyPrice(Player p) => p.Value;
    public int SellPrice(Player p) => Math.Max(1, (int)Math.Round(p.Value * 0.9));

    public bool Owns(string id) => State.OwnedIds.Contains(id);

    public IEnumerable<Player> Market =>
        PlayerDatabase.All.Where(p => !Owns(p.Id));

    public bool Buy(string id)
    {
        if (Owns(id)) return false;
        var p = PlayerDatabase.All.FirstOrDefault(x => x.Id == id);
        if (p is null || State.Money < BuyPrice(p)) return false;
        State.Money -= BuyPrice(p);
        State.OwnedIds.Add(id);
        AutoAssignIfSlotFree(p);
        Commit();
        return true;
    }

    public bool Sell(string id)
    {
        var p = PlayerDatabase.All.FirstOrDefault(x => x.Id == id);
        if (p is null || !Owns(p.Id)) return false;
        State.Money += SellPrice(p);
        State.OwnedIds.Remove(id);
        State.StartingIds.Remove(id);
        Commit();
        return true;
    }

    // Si al comprar hay un hueco libre de su posición en el XI, lo titulariza.
    private void AutoAssignIfSlotFree(Player p)
    {
        var f = Formation;
        int used = Starters.Count(s => s.Pos == p.Pos);
        if (used < f.SlotsFor(p.Pos) && !State.StartingIds.Contains(p.Id))
            State.StartingIds.Add(p.Id);
    }

    // ---------------------------------------------------------------- alineación
    public void SetFormation(string name)
    {
        State.FormationName = name;
        RebuildBestXI();
        Commit();
    }

    public void SetStyle(TeamStyle style) { State.Style = style; Commit(); }

    public bool IsStarter(string id) => State.StartingIds.Contains(id);

    /// <summary>Alterna un jugador dentro/fuera del XI respetando los cupos de la formación.</summary>
    public void ToggleStarter(string id)
    {
        var p = Owned.FirstOrDefault(x => x.Id == id);
        if (p is null) return;

        if (State.StartingIds.Contains(id))
        {
            State.StartingIds.Remove(id);
        }
        else
        {
            var f = Formation;
            int used = Starters.Count(s => s.Pos == p.Pos);
            if (used >= f.SlotsFor(p.Pos))
            {
                // reemplaza al titular más flojo de esa posición
                var weakest = Starters.Where(s => s.Pos == p.Pos)
                                      .OrderBy(s => s.Rating).FirstOrDefault();
                if (weakest is not null) State.StartingIds.Remove(weakest.Id);
            }
            State.StartingIds.Add(id);
        }
        Commit();
    }

    /// <summary>Arma automáticamente el mejor XI posible con el plantel actual.</summary>
    public void RebuildBestXI()
    {
        var f = Formation;
        var chosen = new List<string>();
        foreach (var pos in new[] { Position.GK, Position.DEF, Position.MID, Position.FWD })
        {
            var best = Owned.Where(p => p.Pos == pos)
                            .OrderByDescending(p => p.Rating)
                            .Take(f.SlotsFor(pos))
                            .Select(p => p.Id);
            chosen.AddRange(best);
        }
        State.StartingIds = chosen;
    }

    public void AutoFillXI() { RebuildBestXI(); Commit(); }

    // ---------------------------------------------------------------- torneos
    public Competition? ActiveComp =>
        State.Run is null ? null : CompetitionDatabase.ById(State.Run.CompId);

    public bool CanEnter(Competition c) => State.Money >= c.EntryFee && CanPlay && State.Run is null;

    public void StartTournament(string compId)
    {
        var c = CompetitionDatabase.ById(compId);
        if (!CanEnter(c)) return;

        State.Money -= c.EntryFee;
        var run = new RunState { CompId = compId };

        // Calendario: 3 rivales de grupo + un rival por ronda eliminatoria (más fuertes al final).
        var pool = c.Rivals.OrderBy(_ => _rng.Next()).ToList();
        for (int i = 0; i < 3; i++)
            run.Schedule.Add(Snap(pool[i % pool.Count]));

        var strong = c.Rivals.OrderByDescending(r => r.Strength).ToList();
        int ko = c.KnockoutRounds.Length;
        for (int i = 0; i < ko; i++)
        {
            // rondas más profundas → rivales más fuertes
            int idx = Math.Min(strong.Count - 1, (ko - 1 - i));
            var pick = strong[Math.Clamp(idx + _rng.Next(-1, 2), 0, strong.Count - 1)];
            run.Schedule.Add(Snap(pick));
        }

        State.Run = run;
        Commit();

        static RivalSnapshot Snap(RivalTeam r) => new() { Name = r.Name, Flag = r.Flag, Strength = r.Strength };
    }

    public RivalSnapshot? CurrentOpponent =>
        State.Run is { } r && r.Stage < r.Schedule.Count ? r.Schedule[r.Stage] : null;

    public (bool group, string stageName, int stageIndex, int totalStages) StageInfo()
    {
        var r = State.Run!;
        var c = ActiveComp!;
        bool group = r.Stage < 3;
        string name = group ? $"Fase de grupos · J{r.Stage + 1}" : c.KnockoutRounds[r.Stage - 3];
        return (group, name, r.Stage, 3 + c.KnockoutRounds.Length);
    }

    /// <summary>Simula el próximo partido SIN mutar el estado (para animar antes de aplicar).</summary>
    public MatchResult SimulateNext()
    {
        var run = State.Run!;
        var opp = CurrentOpponent!;
        bool group = run.Stage < 3;

        var result = _engine.Simulate(
            State.ClubName, "⚽", Power, EffectiveStarters,
            opp.Name, opp.Flag, TeamPower.Flat(opp.Strength), allowPenalties: !group);

        LastMatch = result;
        return result;
    }

    /// <summary>La última simulación completa (para la cancha 2D).</summary>
    public MatchTimeline? LastTimeline { get; private set; }

    /// <summary>Planteles usados en la última simulación (para rotular la cancha).</summary>
    public IReadOnlyList<Player> LastHomeStarters { get; private set; } = Array.Empty<Player>();
    public IReadOnlyList<Player> LastAwaySquad { get; private set; } = Array.Empty<Player>();

    /// <summary>Pronóstico previo (Poisson + Elo) del próximo partido.</summary>
    public Prediction PredictNext()
    {
        var opp = CurrentOpponent!;
        return MatchSimulator.Predict(Power, TeamPower.Flat(opp.Strength));
    }

    /// <summary>Simula el próximo partido jugada a jugada (timeline animable). No muta el estado.</summary>
    public MatchTimeline SimulateNextTimeline()
    {
        var run = State.Run!;
        var opp = CurrentOpponent!;
        bool group = run.Stage < 3;

        var home = EffectiveStarters;
        var away = RivalSquad(opp);
        var tl = MatchSimulator.Simulate(
            State.ClubName, "⚽", Power, home,
            opp.Name, opp.Flag, TeamPower.Flat(opp.Strength), away,
            knockout: !group, seed: _rng.Next());

        LastMatch = tl.Result;
        LastTimeline = tl;
        LastHomeStarters = home;
        LastAwaySquad = away;
        return tl;
    }

    private static readonly string[] RivalNames =
        { "Costa", "Vega", "Romero", "Bianchi", "Novak", "Petit", "König", "Silva",
          "Moretti", "Ferreira", "Adé", "Ivanov", "Krause", "Marín", "Blanc", "Sørensen" };

    /// <summary>Genera un XI ficticio para el rival IA, con nombres para los goleadores.</summary>
    private static IReadOnlyList<Player> RivalSquad(RivalSnapshot opp)
    {
        var poss = new[]
        {
            Position.GK, Position.DEF, Position.DEF, Position.DEF, Position.DEF,
            Position.MID, Position.MID, Position.MID, Position.FWD, Position.FWD, Position.FWD
        };
        var list = new List<Player>();
        int seed = Math.Abs(opp.Name.GetHashCode());
        for (int i = 0; i < 11; i++)
        {
            list.Add(new Player
            {
                Id = $"riv-{i}", Name = RivalNames[(seed + i) % RivalNames.Length],
                Nation = opp.Name, Flag = opp.Flag, Pos = poss[i],
                Rating = Math.Clamp(opp.Strength + (i % 3 - 1) * 2, 40, 99),
            });
        }
        return list;
    }

    /// <summary>Aplica premio + progresión del partido ya simulado. Devuelve el dinero ganado.</summary>
    public int ApplyMatch(MatchResult result)
    {
        var run = State.Run!;
        var comp = ActiveComp!;
        bool group = run.Stage < 3;
        int reward = ApplyResult(comp, run, result, group);
        Commit();
        return reward;
    }

    private int ApplyResult(Competition comp, RunState run, MatchResult r, bool group)
    {
        bool win = r.HomeWon;
        bool draw = !r.WentToPenalties && r.HomeGoals == r.AwayGoals;

        // Estadísticas de carrera
        State.MatchesPlayed++;
        State.GoalsFor += r.HomeGoals;
        State.GoalsAgainst += r.AwayGoals;
        if (win) State.Wins++; else if (draw) State.Draws++; else State.Losses++;
        State.BestRatingReached = Math.Max(State.BestRatingReached, Power.Overall);

        // Estadísticas del torneo
        run.GoalsFor += r.HomeGoals;
        run.GoalsAgainst += r.AwayGoals;
        if (r.AwayGoals > 0) run.CleanRun = false;
        if (!(win && r.HomeGoals == 7 && r.AwayGoals == 0)) run.PerfectRun = false;

        int reward = 0;

        if (group)
        {
            run.GroupPlayed++;
            if (win) { run.GroupPoints += 3; reward = comp.PrizePerRound * 2; }
            else if (draw) { run.GroupPoints += 1; reward = comp.PrizePerRound; }

            run.Timeline.Add($"Grupo J{run.Stage + 1}: {Score(r)} vs {r.AwayName}");

            if (run.GroupPlayed >= 3)
            {
                // Clasifica a eliminatorias con 4+ puntos (o 3 pts y saldo de goles positivo)
                bool qualifies = run.GroupPoints >= 4 ||
                                 (run.GroupPoints >= 3 && run.GoalsFor - run.GoalsAgainst >= 0);
                if (qualifies) { run.Stage = 3; reward += comp.PrizePerRound * 2; }
                else { run.Eliminated = true; }
            }
            else run.Stage++;
        }
        else
        {
            int koIndex = run.Stage - 3;
            bool isFinal = run.Stage == (2 + comp.KnockoutRounds.Length);
            string roundName = comp.KnockoutRounds[koIndex];
            run.Timeline.Add($"{roundName}: {Score(r)} vs {r.AwayName}");

            if (win)
            {
                reward = comp.PrizePerRound * (3 + koIndex * 2);
                if (isFinal)
                {
                    run.Champion = true;
                    reward += comp.ChampionPrize;
                }
                else run.Stage++;
            }
            else run.Eliminated = true;
        }

        run.MoneyWon += reward;
        State.Money += reward;

        // Cierre del torneo
        if (run.Champion)
        {
            State.Honours.Add(comp.Name);
            if (run.CleanRun) // campeón sin recibir goles = 7-0 espiritual
                State.AchievedSevenZero = true;
        }

        return reward;
    }

    private static string Score(MatchResult r) =>
        r.WentToPenalties
            ? $"{r.HomeGoals}-{r.AwayGoals} ({r.HomePens}-{r.AwayPens} pen)"
            : $"{r.HomeGoals}-{r.AwayGoals}";

    /// <summary>Cierra el torneo terminado y limpia el estado para volver al mercado.</summary>
    public void ConcludeTournament()
    {
        State.Run = null;
        LastMatch = null;
        Commit();
    }
}
