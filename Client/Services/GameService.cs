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
    private readonly Loc _loc;
    private readonly AuthService _auth;
    private readonly Random _rng = new();
    private CancellationTokenSource? _pushCts;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false,
    };

    public GameState State { get; private set; } = new();
    public event Action? Changed;

    /// <summary>Resultado del último partido jugado (para la pantalla de partido).</summary>
    public MatchResult? LastMatch { get; private set; }

    public GameService(IJSRuntime js, MatchEngine engine, Loc loc, AuthService auth)
    {
        _js = js;
        _engine = engine;
        _loc = loc;
        _auth = auth;
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
        QueueCloudPush();
    }

    // ---------------------------------------------------------------- nube (cuenta)
    /// <summary>Cuánto avanzó una partida. Sirve para no pisar nunca lo más avanzado.</summary>
    private static int Progress(GameState s) =>
        s.MatchesPlayed * 10 + s.Honours.Count * 100 + s.History.Count * 20 + s.OwnedIds.Count;

    /// <summary>Sube el estado a la nube, agrupando ráfagas de cambios (1,5 s).</summary>
    private void QueueCloudPush()
    {
        if (!_auth.IsLoggedIn) return;
        _pushCts?.Cancel();
        _pushCts = new CancellationTokenSource();
        var ct = _pushCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1500, ct);
                if (!ct.IsCancellationRequested) await _auth.PushStateAsync(State);
            }
            catch (TaskCanceledException) { }
            catch { }
        });
    }

    /// <summary>
    /// Une el progreso local con el de la cuenta después de registrarse o entrar.
    /// - Al REGISTRARSE se sube lo que el jugador ya venía jugando (equipo, dinero,
    ///   torneos, estadísticas): así no se pierde nada.
    /// - Al ENTRAR, si la nube ya tiene partida se queda con la MÁS avanzada de las dos.
    /// </summary>
    public async Task SyncAfterAuthAsync(bool justRegistered)
    {
        if (!_auth.IsLoggedIn) return;

        var fetched = await _auth.FetchStateAsync();

        // Si la consulta FALLÓ no sabemos qué hay guardado: no tocamos nada.
        // Subir acá borraría el progreso del jugador.
        if (!fetched.Ok) return;

        var cloud = fetched.State;
        if (cloud is null)
        {
            // La cuenta todavía no tiene partida: subimos lo que el jugador venía jugando.
            await _auth.PushStateAsync(State);
            return;
        }

        if (Progress(cloud) >= Progress(State))
        {
            State = cloud;
            await SaveAsync();
            Changed?.Invoke();
        }
        else
        {
            await _auth.PushStateAsync(State);
        }
    }

    /// <summary>Fuerza un guardado inmediato en la nube (botón "guardar ahora").</summary>
    public Task<bool> PushNowAsync() => _auth.PushStateAsync(State);

    // ---------------------------------------------------------------- plantel
    public IEnumerable<Player> Owned =>
        State.OwnedIds.Select(id => AllPlayers.FirstOrDefault(p => p.Id == id))
                      .Where(p => p is not null)!.Cast<Player>()
                      .Select(Boosted);   // con las mejoras compradas aplicadas

    public IReadOnlyList<Player> Starters =>
        State.StartingIds.Select(id => AllPlayers.FirstOrDefault(p => p.Id == id))
                         .Where(p => p is not null).Cast<Player>()
                         .Select(Boosted).ToList();

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

    // ---------------------------------------------------------------- tokens de mejora
    // Se puede subir de nivel a CUALQUIER jugador, no sólo a los de academia.
    // El precio del token es fijo, pero cada punto cuesta cada vez MÁS tokens:
    // así llevar a un juvenil de 70 a 71 es barato y llevar a Messi de 96 a 97
    // es una inversión enorme. Es lo que mantiene el sistema justo.

    public const int TokenCost = 50;      // millones por token
    public const int MaxRating = 99;

    /// <summary>Cuántos tokens cuesta subir UN punto desde esa fuerza.</summary>
    public static int TokensToUpgrade(int rating) => rating switch
    {
        < 70 => 1,
        < 80 => 2,
        < 85 => 3,
        < 90 => 5,
        < 93 => 8,
        < 95 => 12,
        < 97 => 18,
        < 98 => 25,
        _ => 35,
    };

    public int UpgradeOf(string id) => State.Upgrades.TryGetValue(id, out var v) ? v : 0;

    /// <summary>El jugador con las mejoras que le compraste ya aplicadas.</summary>
    public Player Boosted(Player p)
    {
        int up = UpgradeOf(p.Id);
        if (up <= 0) return p;
        return new Player
        {
            Id = p.Id, Name = p.Name, Nation = p.Nation, Flag = p.Flag, Pos = p.Pos,
            Rating = Math.Min(MaxRating, p.Rating + up), IsLegend = p.IsLegend, Era = p.Era,
        };
    }

    public bool CanBuyTokens(int qty) => qty > 0 && State.Money >= qty * TokenCost;

    public bool BuyTokens(int qty)
    {
        if (!CanBuyTokens(qty)) return false;
        State.Money -= qty * TokenCost;
        State.Tokens += qty;
        Commit();
        return true;
    }

    /// <summary>Sube un punto de fuerza gastando tokens. Tope 99.</summary>
    public bool UpgradePlayer(string id)
    {
        var p = Owned.FirstOrDefault(x => x.Id == id);
        if (p is null || p.Rating >= MaxRating) return false;

        int cost = TokensToUpgrade(p.Rating);
        if (State.Tokens < cost) return false;

        State.Tokens -= cost;
        State.Upgrades[id] = UpgradeOf(id) + 1;
        Commit();
        return true;
    }

    // ---------------------------------------------------------------- torneos juveniles
    // Competencias 5 vs 5 pensadas SOLO para hacer crecer a la cantera: dan mucha
    // experiencia y poca plata. No cansan ni lesionan: son de formación.

    public const int YouthSquadSize = 5;

    // El fútbol juvenil NO da plata: se juega para formar, y cada torneo CUESTA.
    // Antes la Copa Barrial era gratis y repartía premio, así que alcanzaba con
    // apretar el botón mil veces para hacerse millonario y subir a todos gratis.
    public sealed record YouthCup(string Name, int Cost, int RivalStrength, int Matches,
                                  int XpWin, int XpPlay, string Desc);

    public static readonly YouthCup[] YouthCups =
    {
        new("Copa Barrial",         4, 54, 3, 130,  50, "Potreros del barrio. Barata, ideal para empezar."),
        new("Torneo Sub-19",       12, 64, 3, 220,  85, "Categorías juveniles federadas. Ya se juega en serio."),
        new("Copa Federal Juvenil",30, 74, 4, 340, 130, "Lo mejor de las inferiores del país."),
        new("Mundialito Juvenil",  70, 84, 4, 520, 200, "El torneo donde se miran todos los ojeadores del mundo."),
    };

    public sealed record YouthMatch(string Rival, int Us, int Them);
    public sealed record YouthResult(string CupName, List<YouthMatch> Matches, int Won,
                                     int Xp, bool Champion, List<string> LeveledUp);

    /// <summary>Último torneo juvenil jugado, para mostrarlo en pantalla.</summary>
    public YouthResult? LastYouth { get; private set; }

    private static readonly string[] YouthRivals =
        { "Cantera Río Verde", "Juveniles del Puerto", "Escuelita Norte", "Semillero Sur",
          "Academia La Loma", "Inferiores del Valle", "Club Atlético Juvenil", "Sub-19 Costa" };

    public bool CanPlayYouth(YouthCup cup, IReadOnlyCollection<string> ids) =>
        ids.Count == YouthSquadSize && State.Money >= cup.Cost;

    /// <summary>Juega el torneo juvenil completo con los 5 elegidos y reparte experiencia.</summary>
    public YouthResult? PlayYouthCup(YouthCup cup, List<string> ids)
    {
        if (!CanPlayYouth(cup, ids)) return null;

        var equipo = ids.Select(id => State.Academy.FirstOrDefault(a => a.Id == id))
                        .Where(a => a is not null).Cast<AcademyPlayer>().ToList();
        if (equipo.Count != YouthSquadSize) return null;

        State.Money -= cup.Cost;

        var jugadores = equipo.Select(ToPlayer).ToList();
        int fuerza = (int)Math.Round(jugadores.Average(p => p.Rating));
        var rivalSquad = Enumerable.Range(0, YouthSquadSize).Select(i => new Player
        {
            Id = $"yr-{i}", Name = $"Juvenil {i + 1}", Nation = "Rival", Flag = "🌱",
            Pos = (Position)(i % 4), Rating = cup.RivalStrength,
        }).ToList();

        var partidos = new List<YouthMatch>();
        int ganados = 0;

        for (int i = 0; i < cup.Matches; i++)
        {
            string rival = YouthRivals[_rng.Next(YouthRivals.Length)];
            var tl = MatchSimulator.Simulate(
                "Tu cantera", "🎓", TeamPower.Flat(fuerza), jugadores,
                rival, "🌱", TeamPower.Flat(cup.RivalStrength), rivalSquad,
                knockout: false, seed: _rng.Next());

            int us = tl.Result.HomeGoals, them = tl.Result.AwayGoals;
            if (us > them) ganados++;
            partidos.Add(new YouthMatch(rival, us, them));
        }

        // Experiencia: jugar ya suma, ganar suma mucho más. Respeta el techo.
        int xp = ganados * cup.XpWin + (cup.Matches - ganados) * cup.XpPlay;
        var subieron = new List<string>();
        foreach (var a in equipo)
        {
            int antes = a.Rating;
            a.Matches += cup.Matches;
            GrantXp(a, xp);
            if (a.Rating > antes) subieron.Add($"{a.Name} {antes}→{a.Rating}");
        }

        // Sin premio en efectivo: la recompensa es la experiencia de los pibes.
        bool campeon = ganados == cup.Matches;
        LastYouth = new YouthResult(cup.Name, partidos, ganados, xp, campeon, subieron);
        Commit();
        return LastYouth;
    }

    // ---------------------------------------------------------------- ojeadores
    // Cuanto mejor (y más caro) el ojeador, mejores promesas encuentra. Lo que se
    // paga no es lo que el chico rinde HOY, sino hasta dónde puede llegar.

    public sealed record Scout(string Name, int Cost, int MinPot, int MaxPot, string Desc);

    public static readonly Scout[] Scouts =
    {
        new("Ojeador local",         25,  68, 80, "Recorre canchas de barrio. Encuentra chicos aprovechables."),
        new("Ojeador regional",      70,  76, 88, "Cubre todo el país. Aparecen los primeros nombres serios."),
        new("Ojeador internacional",180,  84, 93, "Viaja por el continente. Promesas de selección juvenil."),
        new("Ojeador de élite",     450,  90, 99, "La red que descubre a los cracks antes que nadie."),
    };

    private static readonly string[] ProspectFirst =
        { "Mateo", "Thiago", "Benjamín", "Santino", "Lautaro", "Dylan", "Emiliano", "Bruno",
          "Iker", "Aarón", "Yeferson", "Kevin", "Alexis", "Diego", "Nahuel", "Cristian" };
    private static readonly string[] ProspectLast =
        { "Rojas", "Cabrera", "Medina", "Ferreyra", "Quintero", "Moreno", "Palacios", "Bermúdez",
          "Villalba", "Ocampo", "Zambrano", "Escalante", "Ibarra", "Núñez", "Rivas", "Guerrero" };
    private static readonly (string Nation, string Flag)[] ProspectHome =
    {
        ("Argentina", "🇦🇷"), ("Brasil", "🇧🇷"), ("Uruguay", "🇺🇾"), ("Colombia", "🇨🇴"),
        ("Venezuela", "🇻🇪"), ("Chile", "🇨🇱"), ("México", "🇲🇽"), ("España", "🇪🇸"),
        ("Francia", "🇫🇷"), ("Portugal", "🇵🇹"), ("Nigeria", "🇳🇬"), ("Senegal", "🇸🇳"),
    };

    public bool CanScout(Scout s) => State.Money >= s.Cost;

    /// <summary>Contrata un ojeador: te trae 3 promesas para elegir.</summary>
    public bool SendScout(Scout s)
    {
        if (!CanScout(s)) return false;
        State.Money -= s.Cost;

        for (int i = 0; i < 3; i++)
        {
            int pot = _rng.Next(s.MinPot, s.MaxPot + 1);
            // Cuanto más alto es el techo, más lejos suele estar de alcanzarlo.
            int actual = Math.Clamp(pot - _rng.Next(14, 30), 45, pot - 1);
            var (nation, flag) = ProspectHome[_rng.Next(ProspectHome.Length)];

            State.Prospects.Add(new Prospect
            {
                Id = $"pro-{Guid.NewGuid():N}"[..12],
                Name = $"{ProspectFirst[_rng.Next(ProspectFirst.Length)]} {ProspectLast[_rng.Next(ProspectLast.Length)]}",
                Nation = nation,
                Flag = flag,
                Pos = (Position)_rng.Next(4),
                Rating = actual,
                Potential = pot,
                Age = _rng.Next(16, 20),
                Cost = ProspectPrice(actual, pot),
                ScoutName = s.Name,
            });
        }
        Commit();
        return true;
    }

    /// <summary>Lo que vale una promesa: pesa mucho más el techo que lo que rinde hoy.</summary>
    public static int ProspectPrice(int rating, int potential) =>
        (int)Math.Round(4 + Math.Pow(Math.Max(0, rating - 45), 1.7) / 6.0
                          + Math.Pow(Math.Max(0, potential - 60), 2.1) / 9.0);

    /// <summary>Ficha a la promesa: entra a tu academia con su techo.</summary>
    public bool SignProspect(string id)
    {
        var pr = State.Prospects.FirstOrDefault(x => x.Id == id);
        if (pr is null || State.Money < pr.Cost) return false;

        State.Money -= pr.Cost;
        State.Academy.Add(new AcademyPlayer
        {
            Id = pr.Id, Name = pr.Name, Nation = pr.Nation, Flag = pr.Flag,
            Pos = pr.Pos, Rating = pr.Rating, Potential = pr.Potential, Age = pr.Age,
        });
        State.OwnedIds.Add(pr.Id);
        State.Prospects.Remove(pr);
        Commit();
        return true;
    }

    /// <summary>Descarta una promesa que no te interesa.</summary>
    public void DiscardProspect(string id)
    {
        State.Prospects.RemoveAll(x => x.Id == id);
        Commit();
    }

    // ---------------------------------------------------------------- condición física
    /// <summary>Estado (cansancio, lesión, estadísticas) de un jugador. Lo crea si no existe.</summary>
    public PlayerCondition Cond(string id)
    {
        if (!State.Conditions.TryGetValue(id, out var c))
        {
            c = new PlayerCondition();
            State.Conditions[id] = c;
        }
        return c;
    }

    public bool IsInjured(string id) => State.Conditions.TryGetValue(id, out var c) && c.Injured;
    public int FatigueOf(string id) => State.Conditions.TryGetValue(id, out var c) ? c.Fatigue : 0;

    /// <summary>
    /// El cansancio NO baja el rendimiento hasta que la barra está realmente alta.
    /// Antes restaba desde el primer partido y se sentía roto: ahora hay 60% de
    /// margen libre y recién ahí empieza a pesar (máximo -5 con la barra llena).
    /// </summary>
    public const int FatigueSafe = 60;
    public static int FatiguePenalty(int fatigue) =>
        fatigue <= FatigueSafe ? 0 : (fatigue - FatigueSafe) / 8;

    /// <summary>El jugador tal como rinde HOY: su fuerza menos el desgaste.</summary>
    public Player Effective(Player p)
    {
        int pen = FatiguePenalty(FatigueOf(p.Id));
        if (pen <= 0) return p;
        return new Player
        {
            Id = p.Id, Name = p.Name, Nation = p.Nation, Flag = p.Flag, Pos = p.Pos,
            Rating = Math.Max(35, p.Rating - pen), IsLegend = p.IsLegend, Era = p.Era,
        };
    }

    /// <summary>Titulares disponibles: los lesionados no pueden jugar.</summary>
    public IReadOnlyList<Player> AvailableStarters =>
        Starters.Where(p => !IsInjured(p.Id)).ToList();

    /// <summary>Suplentes sanos que podrían entrar.</summary>
    public IReadOnlyList<Player> AvailableBench =>
        Owned.Where(p => !State.StartingIds.Contains(p.Id) && !IsInjured(p.Id))
             .OrderByDescending(p => p.Rating).ToList();

    /// <summary>El XI real, con los puestos vacíos completados por juveniles de cantera.</summary>
    public IReadOnlyList<Player> EffectiveStarters
    {
        get
        {
            var f = Formation;
            var result = new List<Player>();
            // Los LESIONADOS no entran, y los cansados rinden menos.
            var disponibles = AvailableStarters;
            foreach (var pos in new[] { Position.GK, Position.DEF, Position.MID, Position.FWD })
            {
                var reals = disponibles.Where(p => p.Pos == pos).Take(f.SlotsFor(pos)).ToList();
                result.AddRange(reals.Select(Effective));
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

    // ---------------------------------------------------------------- academia
    // Crear y entrenar jugadores propios. Entrenar cuesta cada vez más caro, así
    // que el dinero del juego avanzado siempre tiene dónde ir.

    public const int AcademyCreateCost = 20;

    /// <summary>Materializa un jugador de academia como <see cref="Player"/> jugable.</summary>
    public static Player ToPlayer(AcademyPlayer a) => new()
    {
        Id = a.Id, Name = a.Name, Nation = a.Nation, Flag = a.Flag,
        Pos = a.Pos, Rating = a.Rating, Era = 2026,
    };

    /// <summary>Todos los jugadores existentes: los del mercado más los tuyos de academia.</summary>
    public IEnumerable<Player> AllPlayers =>
        PlayerDatabase.All.Concat(State.Academy.Select(ToPlayer));

    /// <summary>Cuánto cuesta la próxima sesión de entrenamiento (sube con la fuerza).</summary>
    public static int TrainingCost(int rating)
    {
        double over = Math.Max(0, rating - 50);
        return (int)Math.Round(4 + Math.Pow(over, 1.9) / 12.0);
    }

    /// <summary>Experiencia necesaria para ganar el próximo punto de fuerza.</summary>
    public static int XpNeeded(int rating) => 100 + Math.Max(0, rating - 50) * 18;

    /// <summary>Etapa de la ruta de avances según la fuerza.</summary>
    public static string AcademyTier(int rating) => rating switch
    {
        < 60 => "Juvenil",
        < 70 => "Promesa",
        < 80 => "Titular",
        < 88 => "Figura",
        < 94 => "Crack",
        _ => "Leyenda",
    };

    public bool CanCreateAcademyPlayer => State.Money >= AcademyCreateCost;

    /// <summary>Crea un jugador propio: cuesta dinero y entra directo a tu plantel.</summary>
    public AcademyPlayer? CreateAcademyPlayer(string name, Position pos, string nation)
    {
        if (!CanCreateAcademyPlayer) return null;

        var clean = (name ?? "").Trim();
        if (clean.Length == 0) clean = "Juvenil";
        if (clean.Length > 22) clean = clean[..22];

        var p = new AcademyPlayer
        {
            Id = $"aca-{Guid.NewGuid():N}"[..12],
            Name = clean,
            Nation = string.IsNullOrWhiteSpace(nation) ? "Cantera" : nation.Trim(),
            Pos = pos,
            Rating = 55,
        };

        State.Money -= AcademyCreateCost;
        State.Academy.Add(p);
        State.OwnedIds.Add(p.Id);
        Commit();
        return p;
    }

    /// <summary>Una sesión de entrenamiento: cuesta plata y suma experiencia.</summary>
    public bool TrainAcademyPlayer(string id)
    {
        var a = State.Academy.FirstOrDefault(x => x.Id == id);
        // Una promesa no puede superar su techo: eso es lo que la hace valiosa o no.
        if (a is null || a.Rating >= Math.Min(99, a.Potential)) return false;

        int cost = TrainingCost(a.Rating);
        if (State.Money < cost) return false;

        State.Money -= cost;
        a.Sessions++;
        GrantXp(a, 100);
        Commit();
        return true;
    }

    /// <summary>Suma experiencia y sube de nivel cuando alcanza el umbral.</summary>
    private static void GrantXp(AcademyPlayer a, int xp)
    {
        int techo = Math.Min(99, a.Potential <= 0 ? 99 : a.Potential);
        a.Xp += xp;
        while (a.Rating < techo && a.Xp >= XpNeeded(a.Rating))
        {
            a.Xp -= XpNeeded(a.Rating);
            a.Rating++;
        }
        if (a.Rating >= techo) { a.Rating = techo; a.Xp = 0; }
    }

    // ---------------------------------------------------------------- después del partido
    /// <summary>
    /// Cansancio, lesiones y estadísticas individuales de un partido ya jugado.
    /// Los que jugaron se desgastan; los que miraron descansan. Las lesiones salen
    /// de los eventos del propio partido y el riesgo sube si el jugador venía fundido.
    /// </summary>
    private void ApplyConditions()
    {
        var tl = LastTimeline;
        var jugaron = EffectiveStarters.Where(p => !IsReserve(p)).Select(p => p.Id).ToHashSet();

        // Cuántas jugadas exigentes tuvo cada uno: remates, goles, penales,
        // faltas cometidas y recibidas. El que se rompió el lomo se cansa más.
        var carga = new Dictionary<string, int>();
        if (tl is not null)
        {
            foreach (var e in tl.Events)
            {
                if (e.Type is not (SimEventType.Shot or SimEventType.Goal or SimEventType.Foul
                                   or SimEventType.PenaltyAwarded or SimEventType.Save)) continue;
                foreach (var id in new[] { e.ActorId, e.TargetId })
                    if (!string.IsNullOrEmpty(id) && jugaron.Contains(id))
                        carga[id] = carga.GetValueOrDefault(id) + 1;
            }
        }

        foreach (var id in jugaron)
        {
            var c = Cond(id);
            c.Matches++;
            // Base ~12 + hasta 8 por participación: unos 6 partidos seguidos
            // para llegar al tope, y menos si el jugador tuvo un partido tranquilo.
            int extra = Math.Min(8, carga.GetValueOrDefault(id));
            c.Fatigue = Math.Min(100, c.Fatigue + 11 + extra + _rng.Next(0, 4));
        }

        // Los que no jugaron descansan (y los lesionados van cumpliendo su parte)
        foreach (var p in Owned)
        {
            var c = Cond(p.Id);
            if (!jugaron.Contains(p.Id))
                c.Fatigue = Math.Max(0, c.Fatigue - 34);
            if (c.OutMatches > 0) c.OutMatches--;
        }

        if (tl is null) return;

        // Estadísticas y lesiones a partir de lo que pasó en la cancha (equipo 0 = el tuyo)
        foreach (var e in tl.Events.Where(e => e.Team == 0))
        {
            switch (e.Type)
            {
                case SimEventType.Goal when jugaron.Contains(e.ActorId):
                    Cond(e.ActorId).Goals++;
                    break;
                case SimEventType.Yellow when jugaron.Contains(e.ActorId):
                    Cond(e.ActorId).Yellow++;
                    break;
                case SimEventType.Red when jugaron.Contains(e.ActorId):
                    Cond(e.ActorId).Red++;
                    break;
                case SimEventType.Injury when jugaron.Contains(e.ActorId):
                    var c = Cond(e.ActorId);
                    c.Injuries++;
                    c.OutMatches = Math.Max(c.OutMatches, 1 + _rng.Next(2));   // 1 o 2 partidos
                    break;
            }
        }

        // Venir fundido pasa factura: riesgo extra de lesión muscular
        foreach (var id in jugaron)
        {
            var c = Cond(id);
            if (c.Fatigue >= 90 && !c.Injured && _rng.NextDouble() < 0.02)
            {
                c.Injuries++;
                c.OutMatches = 1;
            }
        }
    }

    /// <summary>Jugar también hace crecer a los tuyos: XP para los de academia que fueron titulares.</summary>
    private void GrantMatchXp()
    {
        foreach (var p in EffectiveStarters)
        {
            var a = State.Academy.FirstOrDefault(x => x.Id == p.Id);
            if (a is null) continue;
            a.Matches++;
            GrantXp(a, 60);
        }
    }

    /// <summary>Vende un jugador de academia (se recupera parte de lo invertido).</summary>
    public void SellAcademyPlayer(string id)
    {
        var a = State.Academy.FirstOrDefault(x => x.Id == id);
        if (a is null) return;
        State.Money += ToPlayer(a).Value / 2;
        State.Academy.Remove(a);
        State.OwnedIds.Remove(id);
        State.StartingIds.Remove(id);
        Commit();
    }

    // ---------------------------------------------------------------- torneos
    public Competition? ActiveComp =>
        State.Run is null ? null : CompetitionDatabase.ById(State.Run.CompId);

    public bool CanEnter(Competition c) => State.Money >= c.EntryFee && CanPlay && State.Run is null;

    // ---------------------------------------------------------------- dificultad viva
    // Los rivales no se quedan quietos: crecen con tu trayectoria y se ponen a tiro
    // si tu equipo quedó muy por encima del torneo. Así ganar nunca es un trámite.

    /// <summary>Refuerzo por trayectoria: cada 2 torneos jugados, +1 (tope +8).</summary>
    public int RivalGrowth => Math.Min(8, State.History.Count / 2);

    /// <summary>Cuánto se acercan los rivales cuando tu equipo supera al torneo (tope +7).</summary>
    public int RivalCatchUp(Competition c) =>
        Math.Clamp((Power.Overall - c.RecommendedStrength) / 3, 0, 7);

    /// <summary>Refuerzo total que reciben los rivales de un torneo.</summary>
    public int RivalBoost(Competition c) => RivalGrowth + RivalCatchUp(c);

    /// <summary>Fuerza real con la que juega un rival, ya escalada.</summary>
    public int ScaledStrength(Competition c, int baseStrength) =>
        Math.Clamp(baseStrength + RivalBoost(c), 40, 99);

    public void StartTournament(string compId)
    {
        var c = CompetitionDatabase.ById(compId);
        if (!CanEnter(c)) return;

        State.Money -= c.EntryFee;
        var run = new RunState { CompId = compId };

        // --- LIGA: jugás contra cada rival una vez y se arma la tabla de posiciones ---
        if (c.Format == CompetitionFormat.League)
        {
            var order = c.Rivals.OrderBy(_ => _rng.Next()).ToList();
            foreach (var riv in order) run.Schedule.Add(Snap(riv));

            run.Table.Add(new TableRow { Name = State.ClubName, Flag = "⚽", Strength = Power.Overall, IsMe = true });
            foreach (var riv in c.Rivals)
                run.Table.Add(new TableRow { Name = riv.Name, Flag = riv.Flag, Strength = ScaledStrength(c, riv.Strength) });

            State.Run = run;
            Commit();
            return;
        }

        // --- COPA: eliminación directa desde la primera ronda, sin fase de grupos ---
        if (c.Format == CompetitionFormat.Knockout)
        {
            int rounds = c.KnockoutRounds.Length;
            var ordered = c.Rivals.OrderBy(r => r.Strength).ToList();
            // los más fuertes quedan para el final
            var picked = ordered.Skip(Math.Max(0, ordered.Count - rounds)).ToList();
            foreach (var riv in picked) run.Schedule.Add(Snap(riv));

            State.Run = run;
            Commit();
            return;
        }

        // Fixture SIN repetir equipos: se sortea todo el torneo de un pool sin reemplazo.
        // - Los MÁS FUERTES se reservan para las eliminatorias (dificultad creciente, final = el mejor).
        // - El grupo se arma con rivales más accesibles del resto.
        int ko = c.KnockoutRounds.Length;
        var byStrength = c.Rivals.OrderBy(r => r.Strength).ToList();

        // Los 'ko' equipos más fuertes → una ronda cada uno (ascendente: primera ronda el más flojo, final el más fuerte).
        int koTake = Math.Min(ko, Math.Max(0, byStrength.Count - 3));
        var koTeams = byStrength.Skip(Math.Max(0, byStrength.Count - koTake)).ToList();
        var koNames = koTeams.Select(t => t.Name).ToHashSet();

        // Candidatos de grupo: el resto, barajado. Nunca reutiliza un rival de eliminatorias.
        var groupPool = byStrength.Where(t => !koNames.Contains(t.Name))
                                  .OrderBy(_ => _rng.Next()).ToList();

        var used = new HashSet<string>();
        void AddUnique(IEnumerable<RivalTeam> src)
        {
            foreach (var r in src)
            {
                if (used.Add(r.Name)) { run.Schedule.Add(Snap(r)); return; }
            }
        }

        // 3 rivales de grupo, distintos entre sí.
        for (int i = 0; i < 3; i++)
            AddUnique(groupPool.Skip(i).Concat(groupPool));

        // Un rival por ronda eliminatoria, del más flojo al más fuerte, sin repetir.
        for (int i = 0; i < ko; i++)
        {
            var order = koTeams.Skip(i).Concat(koTeams).Concat(groupPool);
            AddUnique(order);
        }

        State.Run = run;
        Commit();

        // Los rivales entran al fixture ya escalados a tu nivel actual.
        RivalSnapshot Snap(RivalTeam r) =>
            new() { Name = r.Name, Flag = r.Flag, Strength = ScaledStrength(c, r.Strength) };
    }

    public RivalSnapshot? CurrentOpponent =>
        State.Run is { } r && r.Stage < r.Schedule.Count ? r.Schedule[r.Stage] : null;

    public (bool group, string stageName, int stageIndex, int totalStages) StageInfo()
    {
        var r = State.Run!;
        var c = ActiveComp!;
        if (c.Format == CompetitionFormat.League)
            return (true, string.Format(_loc["Fecha {0} de {1}"], r.Stage + 1, c.LeagueRounds), r.Stage, c.LeagueRounds);

        if (c.Format == CompetitionFormat.Knockout)
        {
            int i = Math.Clamp(r.Stage, 0, c.KnockoutRounds.Length - 1);
            return (false, _loc[FullRound(c.KnockoutRounds[i])], r.Stage, c.KnockoutRounds.Length);
        }

        bool group = r.Stage < 3;
        string name = group
            ? string.Format(_loc["Fase de grupos · Jornada {0}"], r.Stage + 1)
            : _loc[FullRound(c.KnockoutRounds[r.Stage - 3])];
        return (group, name, r.Stage, 3 + c.KnockoutRounds.Length);
    }

    /// <summary>Nombre completo de la ronda (clave en español para localizar) para que se lea claro.</summary>
    public static string FullRound(string round) => round switch
    {
        "Octavos" => "OCTAVOS DE FINAL",
        "Cuartos" => "CUARTOS DE FINAL",
        "Semifinal" => "SEMIFINAL",
        "Final" => "LA FINAL",
        _ => round.ToUpperInvariant()
    };

    /// <summary>Cambia el nombre del club.</summary>
    public void SetClubName(string name)
    {
        var clean = (name ?? "").Trim();
        if (clean.Length == 0) clean = "Mi Equipo";
        State.ClubName = clean.Length > 26 ? clean[..26] : clean;
        Commit();
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

        // Si el partido es a matar o morir se define sí o sí: prórroga y penales.
        // OJO: no alcanza con mirar la etapa, porque en una LIGA la etapa 3 es la
        // fecha 4 (y ahí no hay penales) y en una COPA pura la etapa 0 ya es
        // eliminación directa (y ahí sí tiene que haberlos).
        var comp = ActiveComp!;
        bool knockout = comp.Format switch
        {
            CompetitionFormat.League => false,
            CompetitionFormat.Knockout => true,
            _ => run.Stage >= 3,
        };

        var home = EffectiveStarters;
        var away = RivalSquad(opp);
        var tl = MatchSimulator.Simulate(
            State.ClubName, "⚽", Power, home,
            opp.Name, opp.Flag, TeamPower.Flat(opp.Strength), away,
            knockout: knockout, seed: _rng.Next());

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
        int reward = comp.Format == CompetitionFormat.League
            ? ApplyLeagueResult(comp, run, result)
            : ApplyResult(comp, run, result, group: comp.Format == CompetitionFormat.GroupKnockout && run.Stage < 3);
        Commit();
        return reward;
    }

    // ---------------------------------------------------------------- ligas
    /// <summary>
    /// Una fecha de liga: se registra tu resultado en la tabla y se simulan los
    /// partidos del resto para que la tabla avance de forma creíble. No hay
    /// eliminación: la temporada termina y cobrás según la posición final.
    /// </summary>
    private int ApplyLeagueResult(Competition comp, RunState run, MatchResult r)
    {
        bool win = r.HomeWon;
        bool draw = r.HomeGoals == r.AwayGoals;

        // Carrera
        State.MatchesPlayed++;
        State.GoalsFor += r.HomeGoals;
        State.GoalsAgainst += r.AwayGoals;
        if (win) State.Wins++; else if (draw) State.Draws++; else State.Losses++;
        State.BestRatingReached = Math.Max(State.BestRatingReached, Power.Overall);
        GrantMatchXp();
        ApplyConditions();

        run.GoalsFor += r.HomeGoals;
        run.GoalsAgainst += r.AwayGoals;
        if (r.AwayGoals > 0) run.CleanRun = false;

        // Tabla: mi fila y la del rival
        var me = run.Table.FirstOrDefault(t => t.IsMe);
        var opp = run.Table.FirstOrDefault(t => t.Name == r.AwayName);
        if (me is not null) Record(me, r.HomeGoals, r.AwayGoals);
        if (opp is not null) Record(opp, r.AwayGoals, r.HomeGoals);

        // El resto de la fecha
        SimulateLeagueRound(run, r.AwayName);

        int reward = win ? comp.PrizePerRound * 2 : draw ? comp.PrizePerRound : 0;
        run.Timeline.Add($"Fecha {run.Stage + 1}: {Score(r)} vs {r.AwayName}");
        run.Stage++;

        // ¿Terminó la temporada?
        if (run.Stage >= comp.LeagueRounds)
        {
            int pos = LeaguePosition(run);
            if (pos == 1) { run.Champion = true; reward += comp.ChampionPrize; }
            else
            {
                run.Eliminated = true;   // la temporada terminó sin título
                if (pos == 2) reward += comp.ChampionPrize / 3;
                else if (pos == 3) reward += comp.ChampionPrize / 6;
            }
            run.Timeline.Add($"Posición final: {pos}º");
        }

        run.MoneyWon += reward;
        State.Money += reward;
        if (run.Champion) { State.Honours.Add(comp.Name); if (run.CleanRun) State.AchievedSevenZero = true; }
        return reward;
    }

    /// <summary>Tu posición actual en la tabla (1 = primero).</summary>
    public int LeaguePosition(RunState run) => Standings(run).FindIndex(t => t.IsMe) + 1;

    /// <summary>Tabla ordenada: puntos, luego diferencia de gol, luego goles a favor.</summary>
    public static List<TableRow> Standings(RunState run) =>
        run.Table.OrderByDescending(t => t.Points)
                 .ThenByDescending(t => t.Diff)
                 .ThenByDescending(t => t.GoalsFor)
                 .ThenBy(t => t.Name)
                 .ToList();

    private static void Record(TableRow t, int gf, int ga)
    {
        t.Played++; t.GoalsFor += gf; t.GoalsAgainst += ga;
        if (gf > ga) t.Won++; else if (gf == ga) t.Drawn++; else t.Lost++;
    }

    /// <summary>Empareja a los demás equipos de la fecha y simula sus resultados.</summary>
    private void SimulateLeagueRound(RunState run, string myOpponentName)
    {
        var others = run.Table.Where(t => !t.IsMe && t.Name != myOpponentName)
                              .OrderBy(_ => _rng.Next()).ToList();
        for (int i = 0; i + 1 < others.Count; i += 2)
        {
            var a = others[i];
            var b = others[i + 1];
            int ga = PoissonSample(Lambda(a.Strength, b.Strength));
            int gb = PoissonSample(Lambda(b.Strength, a.Strength));
            Record(a, ga, gb);
            Record(b, gb, ga);
        }
    }

    private static double Lambda(int attack, int defense) =>
        Math.Clamp(1.35 * Math.Exp((attack - defense) / 16.0), 0.2, 4.0);

    private int PoissonSample(double lambda)
    {
        double l = Math.Exp(-lambda), p = 1.0;
        int k = 0;
        do { k++; p *= _rng.NextDouble(); } while (p > l);
        return k - 1;
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
        GrantMatchXp();   // los de academia crecen jugando
        ApplyConditions();  // cansancio, lesiones y estadisticas individuales

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
            // En una copa pura las eliminatorias arrancan en la etapa 0; en el
            // formato con grupos, después de los 3 partidos de la fase inicial.
            int koOffset = comp.Format == CompetitionFormat.Knockout ? 0 : 3;
            int koIndex = Math.Clamp(run.Stage - koOffset, 0, comp.KnockoutRounds.Length - 1);
            bool isFinal = koIndex == comp.KnockoutRounds.Length - 1;
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

    /// <summary>Cierra el torneo terminado, guarda el historial y limpia el estado.</summary>
    public void ConcludeTournament()
    {
        if (State.Run is { } run)
        {
            var c = CompetitionDatabase.ById(run.CompId);
            string outcome = run.Champion
                ? (run.CleanRun ? "🏆 CAMPEÓN (invicto, sin recibir goles)" : "🏆 CAMPEÓN")
                : c.Format == CompetitionFormat.League
                    ? $"terminó {StageReached(run, c)}"       // en liga no te eliminan: terminás en una posición
                    : $"eliminado en {StageReached(run, c)}";
            State.History.Insert(0, $"{c.Emblem} {c.Name} — {outcome} · GF {run.GoalsFor}/GC {run.GoalsAgainst} · +${run.MoneyWon}M");
            if (State.History.Count > 25) State.History.RemoveAt(State.History.Count - 1);
        }
        State.Run = null;
        LastMatch = null;
        Commit();
    }

    private static string StageReached(RunState run, Competition c)
    {
        if (c.Format == CompetitionFormat.League)
            return $"{Standings(run).FindIndex(t => t.IsMe) + 1}º de la tabla";
        int koOffset = c.Format == CompetitionFormat.Knockout ? 0 : 3;
        if (run.Stage < koOffset) return "fase de grupos";
        int ko = run.Stage - koOffset;
        return ko < c.KnockoutRounds.Length ? FullRound(c.KnockoutRounds[ko]).ToLowerInvariant() : "eliminatorias";
    }
}
