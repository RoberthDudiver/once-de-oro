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
            Boxed = p.Boxed, Troll = p.Troll,
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

    // ---------------------------------------------------------------- cajas sorpresa
    // Sumidero de dinero con suerte de por medio. Todas las cajas están calibradas
    // para dar MENOS plata de la que cuestan si revendés lo que sale (los jugadores
    // de caja se revenden a precio de saldo): se abren para buscar un crack, no para
    // hacer negocio. Si dieran ganancia, alcanzaría con abrir y vender en bucle.

    /// <summary>Un tramo de fuerza con su peso relativo dentro de la caja.</summary>
    public sealed record LootBand(int Min, int Max, double Weight);

    public sealed record LootBox(string Key, string Name, string Emoji, int Cost,
                                 string Desc, LootBand[] Bands)
    {
        public double Total => Bands.Sum(b => b.Weight);
        /// <summary>Probabilidad real de un tramo, en % (para mostrarla sin mentir).</summary>
        public double ChanceOf(LootBand b) => b.Weight / Total * 100;
    }

    /// <summary>El payaso: la caja que garantiza un 110 y te estafa con una sonrisa.</summary>
    public const string ClownBox = "payaso";
    public const int TrollRating = 110;
    /// <summary>Lo que el payaso rinde DE VERDAD en la cancha.</summary>
    public const int TrollPlays = 45;

    public static readonly LootBox[] LootBoxes =
    {
        new("basica", "Caja Básica", "📦", 18,
            "Casi siempre un relleno. Casi.",
            new LootBand[] { new(60, 69, 560), new(70, 79, 330), new(80, 89, 90),
                             new(90, 98, 18), new(99, 99, 1.5), new(100, 109, 0.5) }),

        new("plata", "Caja de Plata", "🥈", 70,
            "El termino medio: sale un titular decente.",
            new LootBand[] { new(60, 69, 200), new(70, 79, 420), new(80, 89, 280),
                             new(90, 98, 90), new(99, 99, 7), new(100, 109, 3) }),

        new("oro", "Caja de Oro", "🥇", 200,
            "Acá ya se sueña con una figura.",
            new LootBand[] { new(60, 69, 30), new(70, 79, 220), new(80, 89, 430),
                             new(90, 98, 280), new(99, 99, 28), new(100, 109, 12) }),

        new("leyenda", "Caja de Leyenda", "💎", 500,
            "Cara como pocas cosas. Y todavía puede salirte un 79.",
            new LootBand[] { new(70, 79, 50), new(80, 89, 300), new(90, 98, 530),
                             new(99, 99, 80), new(100, 109, 40) }),

        // La cara de verdad: acá no existe el relleno, el piso es 90. Cuesta más que
        // la de Leyenda justamente por eso: no es que dé mejores 100+, es que NUNCA
        // te clava un 79.
        new("elite", "Caja Élite", "👑", 800,
            "Piso 90. Acá no hay relleno.",
            new LootBand[] { new(90, 98, 740), new(99, 99, 170), new(100, 109, 90) }),

        // Fuera de escala garantizado. Es la segunda más cara del juego y la única
        // que asegura un jugador que además casi no se cansa.
        new("olimpo", "Caja Olimpo", "🏛️", 1000,
            "Sólo dioses: del 100 para arriba, garantizado.",
            new LootBand[] { new(100, 109, 1000) }),

        // La más cara de todas. Y te da un payaso. Ese es el chiste: el cartel más
        // grande y el precio más alto del juego para la peor compra posible.
        new(ClownBox, "Caja del Payaso", "🤡", 1200,
            "GARANTIZADO: un jugador de 110. Palabra de payaso.",
            new LootBand[] { new(TrollRating, TrollRating, 1000) }),
    };

    public bool CanOpenBox(LootBox b) => State.Money >= b.Cost;

    private static readonly string[] ClownNames =
    {
        "Cristiano Ronaldiño", "Lionel Messias", "Zlatan Ibrahimovich", "Neymarcito da Silva",
        "Kylian M'Bapé", "Erling Jaland", "Robertinho Levangolski", "Diego Armando Mandarina",
    };

    /// <summary>
    /// Abre una caja: descuenta la plata, sortea el tramo, genera al jugador y te lo
    /// pone en el plantel. Devuelve al que salió (null si no te alcanzaba).
    /// </summary>
    public Player? OpenBox(LootBox box)
    {
        if (!CanOpenBox(box)) return null;
        State.Money -= box.Cost;

        bool payaso = box.Key == ClownBox;

        // Ruleta ponderada sobre los tramos de la caja.
        double tiro = _rng.NextDouble() * box.Total, acum = 0;
        var band = box.Bands[^1];
        foreach (var b in box.Bands)
        {
            acum += b.Weight;
            if (tiro <= acum) { band = b; break; }
        }

        int rating = _rng.Next(band.Min, band.Max + 1);

        // Del 100 para arriba no se genera un desconocido: sale una VERSIÓN PRIME
        // con nombre y año. Son las únicas cartas de 100+ del juego y no se pueden
        // comprar en el mercado, así que la única forma de tenerlas es esta.
        LootPlayer loot;
        if (!payaso && rating >= 100)
        {
            var prime = ElegirPrime(rating);
            loot = new LootPlayer
            {
                Id = $"box-{Guid.NewGuid():N}"[..12],
                Name = prime.Name, Nation = prime.Nation, Flag = prime.Flag,
                Pos = prime.Pos, Rating = prime.Rating,
                BoxName = box.Name,
            };
        }
        else
        {
            var (nation, flag) = ProspectHome[_rng.Next(ProspectHome.Length)];
            loot = new LootPlayer
            {
                Id = $"box-{Guid.NewGuid():N}"[..12],
                Name = payaso
                    ? ClownNames[_rng.Next(ClownNames.Length)]
                    : $"{ProspectFirst[_rng.Next(ProspectFirst.Length)]} {ProspectLast[_rng.Next(ProspectLast.Length)]}",
                Nation = payaso ? "Circo FC" : nation,
                Flag = payaso ? "🤡" : flag,
                Pos = payaso ? Position.FWD : (Position)_rng.Next(4),
                Rating = rating,
                Troll = payaso,
                BoxName = box.Name,
            };
        }

        State.Loot.Add(loot);
        State.OwnedIds.Add(loot.Id);
        var p = ToPlayer(loot);
        AutoAssignIfSlotFree(p);
        Commit();
        return p;
    }

    /// <summary>
    /// Qué prime te toca para la fuerza que salió. Entre los que están a la misma
    /// distancia se prefieren los que NO tenés: repetir el mismo Messi tres veces
    /// seguidas arruina la ilusión de estar completando un álbum.
    /// </summary>
    private Player ElegirPrime(int rating)
    {
        var candidatos = PrimeDatabase.ClosestTo(rating).ToList();
        var mios = Owned.Select(p => p.Name).ToHashSet();
        var nuevos = candidatos.Where(p => !mios.Contains(p.Name)).ToList();
        var pool = nuevos.Count > 0 ? nuevos : candidatos;
        return pool[_rng.Next(pool.Count)];
    }

    /// <summary>true si ya tenés esa versión prime (se comparan por nombre).</summary>
    public bool OwnsPrime(Player prime) => Owned.Any(p => p.Name == prime.Name);

    // ---------------------------------------------------------------- fusión
    // Tener la versión común de un prime sirve para mejorarlo: sacrificás al Pelé
    // de 96 del mercado y el Pelé (Prime 1970) sube de 108 a 109. Una sola vez por
    // prime, y con techo 109: el 110 sigue siendo exclusivo del payaso.

    public const int PrimeMax = 109;

    /// <summary>
    /// La versión común que podés sacrificar para mejorar a este prime, o null si
    /// no se puede (no es prime tuyo, ya lo fusionaste, está en el techo, o no
    /// tenés al jugador común).
    /// </summary>
    public Player? FuseSource(Player prime)
    {
        var loot = State.Loot.FirstOrDefault(l => l.Id == prime.Id);
        if (loot is null || loot.Troll || loot.Fused || loot.Rating >= PrimeMax) return null;
        if (!PrimeDatabase.IsPrimeName(loot.Name)) return null;

        var comun = PrimeDatabase.BaseName(loot.Name);
        return Owned.FirstOrDefault(p => p.Name == comun && p.Id != prime.Id);
    }

    public bool CanFuse(Player prime) => FuseSource(prime) is not null;

    /// <summary>true si a ese prime ya se lo mejoró con su versión común.</summary>
    public bool IsFused(string id) => State.Loot.FirstOrDefault(l => l.Id == id)?.Fused == true;

    /// <summary>Sacrifica la versión común: el prime sube un punto y ella se va.</summary>
    public bool Fuse(string primeId)
    {
        var loot = State.Loot.FirstOrDefault(l => l.Id == primeId);
        if (loot is null) return false;

        var fuente = FuseSource(ToPlayer(loot));
        if (fuente is null) return false;

        State.OwnedIds.Remove(fuente.Id);
        State.StartingIds.Remove(fuente.Id);
        State.Conditions.Remove(fuente.Id);
        State.Upgrades.Remove(fuente.Id);

        loot.Rating = Math.Min(PrimeMax, loot.Rating + 1);
        loot.Fused = true;
        Commit();
        return true;
    }

    /// <summary>Materializa un jugador de caja como <see cref="Player"/> jugable.</summary>
    public static Player ToPlayer(LootPlayer l) => new()
    {
        Id = l.Id, Name = l.Name, Nation = l.Nation, Flag = l.Flag,
        Pos = l.Pos, Rating = l.Rating, Era = 2026,
        IsLegend = !l.Troll && l.Rating >= 99,
        Boxed = true, Troll = l.Troll,
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

    /// <summary>Fechas que le quedan de suspensión por tarjetas. 0 = puede jugar.</summary>
    public const int RedBanMatches = 1;
    public const int YellowsForBan = 5;

    public int BanLeft(string id) => State.Conditions.TryGetValue(id, out var c) ? c.Suspended : 0;
    public bool IsSuspended(string id) => BanLeft(id) > 0;

    // ---------------------------------------------------------------- descanso
    // Podés mandar a un jugador a concentrar: 1 minuto REAL sin poder jugar y
    // vuelve entero. El costo es el tiempo, no la plata, y por eso el descanso lo
    // deja afuera del equipo mientras dura: si pudiera jugar igual, sería gratis
    // y el cansancio no significaría nada.

    public const int RestMinutes = 1;

    /// <summary>Cuánto le falta para volver de la concentración. Cero = disponible.</summary>
    public TimeSpan RestLeft(string id)
    {
        if (!State.Conditions.TryGetValue(id, out var c) || c.RestUntil is null) return TimeSpan.Zero;
        var falta = c.RestUntil.Value - DateTime.UtcNow;
        return falta > TimeSpan.Zero ? falta : TimeSpan.Zero;
    }

    public bool IsResting(string id) => RestLeft(id) > TimeSpan.Zero;

    /// <summary>
    /// El cansancio de HOY. Si ya cumplió el descanso devuelve 0 aunque todavía no
    /// hayamos guardado el cambio: la lectura nunca puede mentir por un timing.
    /// </summary>
    public int FatigueOf(string id)
    {
        if (!State.Conditions.TryGetValue(id, out var c)) return 0;
        if (c.RestUntil is not null && DateTime.UtcNow >= c.RestUntil.Value) return 0;
        return c.Fatigue;
    }

    /// <summary>Manda a concentrar. Sale del equipo hasta que se cumpla el minuto.</summary>
    public void Rest(string id)
    {
        var c = Cond(id);
        if (c.Injured || c.Fatigue == 0) return;
        c.RestUntil = DateTime.UtcNow.AddMinutes(RestMinutes);
        Commit();
    }

    /// <summary>Manda a concentrar a todos los que lo necesitan.</summary>
    public int RestAll()
    {
        int n = 0;
        foreach (var p in Owned)
        {
            var c = Cond(p.Id);
            if (c.Injured || c.Fatigue == 0 || IsResting(p.Id)) continue;
            c.RestUntil = DateTime.UtcNow.AddMinutes(RestMinutes);
            n++;
        }
        if (n > 0) Commit();
        return n;
    }

    /// <summary>
    /// Cierra los descansos cumplidos: pone el tanque a full y limpia la marca.
    /// Devuelve true si algo cambió, para refrescar la pantalla.
    /// </summary>
    public bool SettleRests()
    {
        bool algo = false;
        foreach (var c in State.Conditions.Values)
        {
            if (c.RestUntil is null || DateTime.UtcNow < c.RestUntil.Value) continue;
            c.Fatigue = 0;
            c.RestUntil = null;
            algo = true;
        }
        if (algo) Commit();
        return algo;
    }

    /// <summary>
    /// El cansancio NO baja el rendimiento hasta que la barra está realmente alta.
    /// Antes restaba desde el primer partido y se sentía roto: ahora hay 60% de
    /// margen libre y recién ahí empieza a pesar (máximo -5 con la barra llena).
    /// </summary>
    public const int FatigueSafe = 60;
    public static int FatiguePenalty(int fatigue) =>
        fatigue <= FatigueSafe ? 0 : (fatigue - FatigueSafe) / 8;

    /// <summary>
    /// Cuánto le pesa un partido según lo que es. El crack está mejor preparado y
    /// se recupera antes, así que aguanta muchas más fechas seguidas:
    ///   105-110 → no se cansa nunca (son de otro planeta)
    ///   100-104 → se cansan muchísimo más lento que el resto
    ///   y de ahí para abajo, cuanto más flojo, más le cuesta el partido.
    /// </summary>
    /// <summary>
    /// Cuánto castiga el estilo. Ir al ataque exige correr mucho más; meterse
    /// atrás cansa menos. Es lo que hace que cambiar el planteo tenga precio.
    /// </summary>
    public static double StyleFatigue(TeamStyle s) => s switch
    {
        TeamStyle.Ofensivo => 1.30,
        TeamStyle.Defensivo => 0.75,
        _ => 1.00,
    };

    /// <summary>
    /// El estilo de CADA tiempo. Si en el entretiempo pasás de defensivo a
    /// ofensivo, el segundo tiempo cansa más que el primero, y al revés.
    /// </summary>
    public TeamStyle StylePrimerTiempo { get; private set; } = TeamStyle.Equilibrado;
    public TeamStyle StyleSegundoTiempo { get; private set; } = TeamStyle.Equilibrado;

    public static double FatigueFactor(int rating) => rating switch
    {
        >= 105 => 0.00,
        >= 100 => 0.25,
        >= 95 => 0.55,
        >= 90 => 0.70,
        >= 80 => 0.85,
        _ => 1.00,
    };

    /// <summary>El jugador tal como rinde HOY: su fuerza menos el desgaste.</summary>
    public Player Effective(Player p)
    {
        int pen = FatiguePenalty(FatigueOf(p.Id));
        // El payaso cotiza 110 pero juega como un 45: acá se cae la careta, y como
        // TODO el juego pasa por Effective (fuerza del equipo, Auto XI, simulación),
        // la broma vale en la cancha y no sólo en la carta.
        if (pen <= 0 && !p.Troll) return p;
        int baseRating = p.Troll ? TrollPlays : p.Rating;
        return new Player
        {
            Id = p.Id, Name = p.Name, Nation = p.Nation, Flag = p.Flag, Pos = p.Pos,
            Rating = Math.Max(35, baseRating - pen), IsLegend = p.IsLegend, Era = p.Era,
            Boxed = p.Boxed, Troll = p.Troll,
        };
    }

    /// <summary>
    /// Nombres bloqueados por la sala online: el rival ya los fichó y no pueden
    /// estar en los dos equipos. NO se guarda con la partida — vale sólo mientras
    /// estás en la sala, y la pantalla de Online lo limpia al salir. Si esto
    /// quedara colgado, tu equipo de carrera jugaría debilitado sin explicación.
    /// </summary>
    public HashSet<string> BlockedNames { get; private set; } = new();

    public void SetBlockedNames(IEnumerable<string> nombres)
    {
        var nuevo = nombres.ToHashSet();
        if (nuevo.SetEquals(BlockedNames)) return;
        BlockedNames = nuevo;
        Changed?.Invoke();
    }

    public void ClearBlockedNames() => SetBlockedNames(Array.Empty<string>());

    private bool Blocked(Player p) => BlockedNames.Count > 0 && BlockedNames.Contains(p.Name);

    /// <summary>No puede jugar: está lesionado o concentrado descansando.</summary>
    public bool IsOut(string id) => IsInjured(id) || IsSuspended(id) || IsResting(id);

    /// <summary>Titulares disponibles: los lesionados y los que descansan no juegan.</summary>
    public IReadOnlyList<Player> AvailableStarters =>
        Starters.Where(p => !IsOut(p.Id) && !Blocked(p)).ToList();

    /// <summary>Suplentes disponibles que podrían entrar.</summary>
    public IReadOnlyList<Player> AvailableBench =>
        Owned.Where(p => !State.StartingIds.Contains(p.Id) && !IsOut(p.Id) && !Blocked(p))
             .OrderByDescending(EffectiveRating).ThenByDescending(p => p.Rating).ToList();

    /// <summary>El XI real, con los puestos vacíos completados por juveniles de cantera.</summary>
    public IReadOnlyList<Player> EffectiveStarters
    {
        get
        {
            var f = Formation;
            var result = new List<Player>();
            // Los LESIONADOS no entran, y los cansados rinden menos.
            var disponibles = AvailableStarters;
            var banco = AvailableBench;
            foreach (var pos in new[] { Position.GK, Position.DEF, Position.MID, Position.FWD })
            {
                var reals = disponibles.Where(p => p.Pos == pos).Take(f.SlotsFor(pos)).ToList();

                // Si un titular se lesionó, el puesto lo tapa el mejor suplente SANO
                // de esa posición. Antes salía un juvenil de 53 aunque tuvieras un 90
                // sentado en el banco: el equipo se debilitaba sin razón.
                foreach (var s in banco.Where(p => p.Pos == pos)
                                       .OrderByDescending(EffectiveRating))
                {
                    if (reals.Count >= f.SlotsFor(pos)) break;
                    if (reals.Any(r => r.Id == s.Id)) continue;
                    reals.Add(s);
                }

                result.AddRange(reals.Select(Effective));
                // Recién si no queda NADIE sano en el plantel para ese puesto, cantera.
                for (int i = reals.Count; i < f.SlotsFor(pos); i++)
                    result.Add(ReserveFor(pos, i));
            }
            return result;
        }
    }

    /// <summary>
    /// La fuerza del equipo CON las órdenes del DT aplicadas. Los efectos de los
    /// once se suman y se dividen por 3: si le pedís a los tres delanteros que se
    /// desmarquen (+4 cada uno) el equipo gana 4 de ataque, no 12. Así una orden
    /// se nota pero no rompe el partido.
    /// </summary>
    public TeamPower Power
    {
        get
        {
            var xi = EffectiveStarters;
            var p = MatchEngine.PowerOf(xi, State.Style);
            int atk = 0, def = 0;
            foreach (var pl in xi)
            {
                var (a, d) = EfectoOrden(pl);
                atk += a; def += d;
            }
            if (atk == 0 && def == 0) return p;
            int C(int v) => Math.Clamp(v, 30, 109);
            return new TeamPower(p.Overall, C(p.Attack + atk / 3), C(p.Defense + def / 3));
        }
    }

    /// <summary>Siempre se puede jugar: los huecos se cubren con cantera.</summary>
    public bool CanPlay => true;

    /// <summary>Cuántos titulares son juveniles de cantera (huecos sin fichar).</summary>
    public int ReserveCount => EffectiveStarters.Count(IsReserve);

    /// <summary>true si los 11 titulares son jugadores reales (sin cantera).</summary>
    public bool HasFullRealXI => ReserveCount == 0;

    // ---------------------------------------------------------------- mercado
    public int BuyPrice(Player p) => p.Value;

    /// <summary>
    /// Lo que te dan por venderlo. Los jugadores de caja se revenden a precio de
    /// saldo (30%): si te devolvieran el 90% como los del mercado, abrir cajas
    /// baratas y vender lo que sale sería una impresora de billetes.
    /// </summary>
    public int SellPrice(Player p) =>
        Math.Max(1, (int)Math.Round(p.Value * (p.Boxed ? 0.30 : 0.9)));

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
        // Se busca en TODO lo que puede ser tuyo, no sólo en la base del mercado:
        // los que salen de una caja y los de tu academia también son vendibles, y
        // antes el botón simplemente no hacía nada con ellos.
        var p = Owned.FirstOrDefault(x => x.Id == id);
        if (p is null) return false;

        State.Money += SellPrice(p);
        State.OwnedIds.Remove(id);
        State.StartingIds.Remove(id);

        // Y se borra del lugar donde vivía, si no quedaba como fantasma: fuera del
        // plantel pero todavía ocupando lugar en la partida guardada.
        State.Loot.RemoveAll(l => l.Id == id);
        State.Academy.RemoveAll(a => a.Id == id);
        State.Conditions.Remove(id);
        State.Upgrades.Remove(id);

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

    // ---------------------------------------------------------------- estadio
    // Los colores del club y las gradas. Es puro adorno, pero es lo que hace que
    // la cancha se sienta TUYA y no la misma de todos.

    /// <summary>
    /// Un tamaño de estadio. Cuanta más gente entra, más recauda por partido.
    /// El ingreso va en MILES para poder expresar los 500 mil de la tribuna
    /// más chica, que en millones enteros se perdería al redondear.
    /// </summary>
    // ---------------------------------------------------------------- roles y ordenes
    public sealed record Rol(string Key, string Nombre, string Icono, string Desc);

    public static readonly Rol[] Roles =
    {
        new("cap",   "Capitán",           "🎖️", "Lleva la cinta."),
        new("pen",   "Penales",           "🎯", "Patea los penales del partido."),
        new("libre", "Tiros libres",      "⚽", "Le pega a los libres directos."),
        new("cder",  "Córner derecho",    "⛳", "Los tira desde la banda derecha."),
        new("cizq",  "Córner izquierdo",  "⛳", "Los tira desde la banda izquierda."),
    };

    public string RolDe(string key) => key switch
    {
        "cap" => State.Roles.CaptainId,
        "pen" => State.Roles.PenaltyId,
        "libre" => State.Roles.FreeKickId,
        "cder" => State.Roles.CornerRightId,
        _ => State.Roles.CornerLeftId,
    };

    public void SetRol(string key, string playerId)
    {
        switch (key)
        {
            case "cap": State.Roles.CaptainId = playerId; break;
            case "pen": State.Roles.PenaltyId = playerId; break;
            case "libre": State.Roles.FreeKickId = playerId; break;
            case "cder": State.Roles.CornerRightId = playerId; break;
            default: State.Roles.CornerLeftId = playerId; break;
        }
        Commit();
    }

    /// <summary>Una orden táctica: qué hace el jugador y qué le cambia.</summary>
    public sealed record Orden(string Key, string Nombre, string Desc,
                               int Ataque, int Defensa, bool SoloLateral = false);

    /// <summary>
    /// Las órdenes disponibles según el puesto. Los números son lo que le suma o
    /// le resta a ESE jugador en el cálculo de la fuerza del equipo: pedirle a un
    /// lateral que suba da ataque y cuesta defensa, y al revés.
    /// </summary>
    public static Orden[] OrdenesDe(Position pos) => pos switch
    {
        Position.FWD => new[]
        {
            new Orden("desmarque", "Desmarcarse",
                "Rompe la línea defensiva y busca los balones en profundidad.", 4, -1),
            new Orden("centro", "Quedarse en el centro",
                "No se abre a las bandas: siempre de referencia en el área.", 3, 0),
        },
        Position.MID => new[]
        {
            new Orden("atras", "Quedarse atrás al atacar",
                "No sube: cubre la zona de volantes para evitar contragolpes.", -2, 5),
            new Orden("cortar", "Cortar líneas de pase",
                "Intercepta en vez de ir a chocar.", 0, 4),
        },
        Position.DEF => new[]
        {
            new Orden("atras", "Quedarse atrás al atacar",
                "No abandona su posición defensiva.", -1, 4),
            new Orden("subir", "Avance por la banda",
                "Sube a dar amplitud cuando tenés la pelota.", 5, -3, SoloLateral: true),
        },
        _ => Array.Empty<Orden>(),
    };

    public string OrdenDe(string id) => State.Orders.GetValueOrDefault(id, "");

    public void SetOrden(string id, string key)
    {
        if (string.IsNullOrEmpty(key)) State.Orders.Remove(id);
        else State.Orders[id] = key;
        Commit();
    }

    /// <summary>Lo que la orden le cambia a un jugador, si tiene alguna puesta.</summary>
    public (int atk, int def) EfectoOrden(Player p)
    {
        string k = OrdenDe(p.Id);
        if (string.IsNullOrEmpty(k)) return (0, 0);
        var o = OrdenesDe(p.Pos).FirstOrDefault(x => x.Key == k);
        return o is null ? (0, 0) : (o.Ataque, o.Defensa);
    }

    public sealed record StadiumTier(string Key, string Name, int Capacity,
                                     int IncomeK, int Cost, int GradaPx, string Desc);

    /// <summary>
    /// Calibrado para que cada salto se pague solo en ~14 partidos: así comprar
    /// siempre conviene a la larga, pero nunca es plata gratis en el momento.
    /// </summary>
    public static readonly StadiumTier[] StadiumTiers =
    {
        new("popular",    "Tribuna Popular",     8_000,     500,     0, 18,
            "La cancha del barrio. Con lo que entra apenas se pagan los focos."),
        new("municipal",  "Estadio Municipal",  25_000,   3_000,    40, 24,
            "Tablones de verdad y una popular que empuja."),
        new("ciudad",     "Estadio de Ciudad",  45_000,  12_000,   150, 30,
            "Ya se agotan las entradas una semana antes."),
        new("monumental", "Monumental",         70_000,  35_000,   500, 36,
            "Tres bandejas. El rival entra intimidado."),
        new("coloso",     "Coloso",             90_000,  80_000, 1_200, 42,
            "De los que salen en la tele del mundo entero."),
        new("templo",     "Templo del Fútbol", 120_000, 200_000, 3_000, 50,
            "El estadio más grande que se puede tener. Se llena solo."),
    };

    public StadiumTier Stadium =>
        StadiumTiers.FirstOrDefault(t => t.Key == State.Stadium) ?? StadiumTiers[0];

    /// <summary>El siguiente escalón, o null si ya tenés el más grande.</summary>
    public StadiumTier? NextStadium()
    {
        int i = Array.FindIndex(StadiumTiers, t => t.Key == Stadium.Key);
        return i >= 0 && i + 1 < StadiumTiers.Length ? StadiumTiers[i + 1] : null;
    }

    public bool OwnsStadium(StadiumTier t) =>
        Array.FindIndex(StadiumTiers, x => x.Key == State.Stadium) >=
        Array.FindIndex(StadiumTiers, x => x.Key == t.Key);

    public bool CanBuyStadium(StadiumTier t) => !OwnsStadium(t) && State.Money >= t.Cost;

    public bool BuyStadium(StadiumTier t)
    {
        if (!CanBuyStadium(t)) return false;
        State.Money -= t.Cost;
        State.Stadium = t.Key;
        Commit();
        return true;
    }

    /// <summary>
    /// Cobra la entrada de un partido. Devuelve los millones que entraron ahora
    /// (puede ser 0 con la tribuna chica: los 500 mil quedan guardados y recién
    /// al segundo partido completan el millón).
    /// </summary>
    /// <summary>Lo que entró por la puerta en el último partido, en millones.</summary>
    public int LastGateM { get; private set; }

    private int CobrarEntrada()
    {
        State.GateBankK += Stadium.IncomeK;
        int millones = State.GateBankK / 1000;
        State.GateBankK %= 1000;
        State.Money += millones;
        return millones;
    }

    public sealed record StandStyle(string Key, string Name, string Desc);

    public static readonly StandStyle[] StandStyles =
    {
        new("clasica",  "Clásica",  "Tribuna llena en el color del club."),
        new("bicolor",  "Bicolor",  "Franjas alternadas con los dos colores."),
        new("mosaico",  "Mosaico",  "El clásico mosaico de la hinchada."),
        new("nocturna", "Nocturna", "Estadio a oscuras con celulares prendidos."),
        new("vacia",    "Vacía",    "A puertas cerradas, sin público."),
    };

    /// <summary>Paletas listas, para no tener que pelearse con el selector de color.</summary>
    public static readonly (string Name, string P, string S)[] Kits =
    {
        ("Oro",       "#f5c542", "#12203c"),
        ("Sangre",    "#e23b3b", "#1a1a1a"),
        ("Cielo",     "#4ea3ff", "#ffffff"),
        ("Verde",     "#2fbf6b", "#0e2a1c"),
        ("Violeta",   "#9b6bff", "#1a1030"),
        ("Naranja",   "#ff8a3d", "#2a1405"),
        ("Rosa",      "#ff5fae", "#2a0f1d"),
        ("Blanco",    "#f2f2f2", "#111111"),
    };

    public void SetColors(string primary, string secondary)
    {
        if (!string.IsNullOrWhiteSpace(primary)) State.Primary = primary;
        if (!string.IsNullOrWhiteSpace(secondary)) State.Secondary = secondary;
        Commit();
    }

    public void SetStands(string key)
    {
        if (StandStyles.Any(s => s.Key == key)) { State.Stands = key; Commit(); }
    }

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
            // El que no puede jugar (lesionado o concentrado) no entra al XI: si lo
            // dejábamos, ocupaba el puesto y después no jugaba, que es justo el
            // hueco que queríamos evitar.
            if (IsOut(id)) return;

            var f = Formation;
            int used = Starters.Count(s => s.Pos == p.Pos);
            if (used >= f.SlotsFor(p.Pos))
            {
                // reemplaza al titular más flojo de esa posición
                // el más flojo de HOY (cansancio incluido), no el de menor fuerza nominal
                var weakest = Starters.Where(s => s.Pos == p.Pos)
                                      .OrderBy(EffectiveRating).ThenBy(s => s.Rating).FirstOrDefault();
                if (weakest is not null) State.StartingIds.Remove(weakest.Id);
            }
            State.StartingIds.Add(id);
        }
        Commit();
    }

    /// <summary>
    /// Fuerza con la que un jugador va a jugar HOY: la suya menos lo que le
    /// descuenta el cansancio. Es la que hay que mirar para armar el equipo,
    /// porque un 92 fundido puede rendir menos que un 85 descansado.
    /// </summary>
    public int EffectiveRating(Player p) => Effective(p).Rating;

    /// <summary>Arma automáticamente el mejor XI posible con el plantel actual.</summary>
    public void RebuildBestXI()
    {
        var f = Formation;
        var chosen = new List<string>();
        foreach (var pos in new[] { Position.GK, Position.DEF, Position.MID, Position.FWD })
        {
            // Los LESIONADOS quedan afuera: si los elegíamos, ocupaban el puesto
            // pero después no podían jugar, y el hueco salía vacío a la cancha.
            // Y ordenamos por fuerza EFECTIVA, no por la nominal: así un titular
            // cansado pierde el puesto contra un suplente fresco que hoy rinde más.
            var best = Owned.Where(p => p.Pos == pos && !IsOut(p.Id))
                            .OrderByDescending(EffectiveRating)
                            .ThenByDescending(p => p.Rating)     // a igual rendimiento, el mejor jugador
                            .ThenBy(p => FatigueOf(p.Id))        // y entre iguales, el más descansado
                            .Take(f.SlotsFor(pos))
                            .Select(p => p.Id);
            chosen.AddRange(best);
        }
        State.StartingIds = chosen;
    }

    public void AutoFillXI() { RebuildBestXI(); Commit(); }

    /// <summary>
    /// Rearma el mejor XI posible SIN usar a los jugadores de esos nombres. Se usa
    /// en el online cuando el rival ya fichó a alguien tuyo: sacarlo a mano no
    /// alcanzaba, porque el hueco lo volvía a tapar el mismo jugador desde el banco.
    /// </summary>
    public void RebuildAvoiding(ISet<string> nombresProhibidos)
    {
        var f = Formation;
        var chosen = new List<string>();
        foreach (var pos in new[] { Position.GK, Position.DEF, Position.MID, Position.FWD })
        {
            var best = Owned.Where(p => p.Pos == pos && !IsOut(p.Id) && !nombresProhibidos.Contains(p.Name))
                            .OrderByDescending(EffectiveRating)
                            .ThenByDescending(p => p.Rating)
                            .Take(f.SlotsFor(pos))
                            .Select(p => p.Id);
            chosen.AddRange(best);
        }
        State.StartingIds = chosen;
        Commit();
    }

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
        PlayerDatabase.All.Concat(State.Academy.Select(ToPlayer))
                          .Concat(State.Loot.Select(ToPlayer));

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

            // Base + lo que le costó el partido. El que se rompió el lomo se cansa más.
            int extra = Math.Min(6, carga.GetValueOrDefault(id));
            int bruto = 8 + extra + _rng.Next(0, 3);

            int rating = Owned.FirstOrDefault(p => p.Id == id)?.Rating ?? 70;
            // Los dos tiempos pueden haberse jugado con planteos distintos: se
            // promedia lo que costó cada mitad.
            double estilo = (StyleFatigue(StylePrimerTiempo) + StyleFatigue(StyleSegundoTiempo)) / 2.0;
            c.Fatigue = Math.Min(100, c.Fatigue + (int)Math.Round(bruto * FatigueFactor(rating) * estilo));
        }

        // Los que no jugaron descansan (y los lesionados van cumpliendo su parte)
        foreach (var p in Owned)
        {
            var c = Cond(p.Id);
            if (!jugaron.Contains(p.Id))
                c.Fatigue = Math.Max(0, c.Fatigue - 34);
            // Se cumple una fecha de la lesión y otra de la suspensión. Va ANTES de
            // procesar las tarjetas de este partido, así el que ve la roja hoy se
            // pierde el próximo y no éste, que ya jugó.
            if (c.OutMatches > 0) c.OutMatches--;
            if (c.Suspended > 0) c.Suspended--;
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
                // Las tarjetas ahora PESAN: antes sólo se contaban y el expulsado
                // jugaba el partido siguiente como si nada.
                case SimEventType.Yellow when jugaron.Contains(e.ActorId):
                    var cy = Cond(e.ActorId);
                    cy.Yellow++;
                    cy.YellowStreak++;
                    if (cy.YellowStreak >= YellowsForBan)      // 5 amarillas = una fecha
                    {
                        cy.YellowStreak = 0;
                        cy.Suspended = Math.Max(cy.Suspended, 1);
                    }
                    break;
                case SimEventType.Red when jugaron.Contains(e.ActorId):
                    var cr = Cond(e.ActorId);
                    cr.Red++;
                    cr.YellowStreak = 0;                       // la roja limpia la cuenta
                    cr.Suspended = Math.Max(cr.Suspended, RedBanMatches);
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

        // Se simula SOLO el primer tiempo: en el entretiempo el DT puede meter
        // cambios y cambiar el planteo, y recién ahí se juega el segundo.
        _rival = opp;
        _knockout = knockout;
        _awaySquad = away;
        StylePrimerTiempo = State.Style;
        StyleSegundoTiempo = State.Style;
        _cambiosHechos = 0;

        var tl = MatchSimulator.Simulate(
            State.ClubName, "⚽", Power, home,
            opp.Name, opp.Flag, TeamPower.Flat(opp.Strength), away,
            knockout: knockout, seed: _rng.Next(),
            homeRoles: State.Roles,    // tus designados patean penales, libres y corners
            half: MatchHalf.First);

        LastMatch = tl.Result;
        LastTimeline = tl;
        LastHomeStarters = home;
        LastAwaySquad = away;
        return tl;
    }

    // ---------------------------------------------------------------- entretiempo
    // Datos del partido en curso que hacen falta para reanudarlo en el 2T.
    private RivalSnapshot? _rival;
    private bool _knockout;
    private IReadOnlyList<Player> _awaySquad = Array.Empty<Player>();
    private int _cambiosHechos;

    public const int MaxCambios = 3;
    public int CambiosHechos => _cambiosHechos;
    public int CambiosDisponibles => MaxCambios - _cambiosHechos;

    /// <summary>Cambia el planteo en el vestuario. Afecta al 2T y a cuánto se cansan.</summary>
    public void SetStyleSegundoTiempo(TeamStyle s)
    {
        State.Style = s;
        StyleSegundoTiempo = s;
        Commit();
    }

    /// <summary>
    /// Mete un cambio: sale uno del XI y entra un suplente. Sólo hasta tres, como
    /// en el fútbol de verdad.
    /// </summary>
    /// <summary>
    /// Ajuste del XI en la PREVIA: cambia un titular por un suplente sin gastar
    /// cambios ni tocar el limite de tres. Es armar el equipo antes de arrancar.
    /// </summary>
    public bool SwapStarter(string saleId, string entraId)
    {
        if (!State.StartingIds.Contains(saleId)) return false;
        var entra = Owned.FirstOrDefault(p => p.Id == entraId);
        if (entra is null || State.StartingIds.Contains(entraId) || IsOut(entraId)) return false;
        State.StartingIds[State.StartingIds.IndexOf(saleId)] = entraId;
        Commit();
        return true;
    }

    public bool Cambiar(string saleId, string entraId)
    {
        if (_cambiosHechos >= MaxCambios) return false;
        if (!State.StartingIds.Contains(saleId)) return false;
        var entra = Owned.FirstOrDefault(p => p.Id == entraId);
        if (entra is null || State.StartingIds.Contains(entraId) || IsOut(entraId)) return false;

        int i = State.StartingIds.IndexOf(saleId);
        State.StartingIds[i] = entraId;
        _cambiosHechos++;
        Commit();
        return true;
    }

    /// <summary>
    /// Juega el segundo tiempo con el equipo y el planteo que quedaron. Devuelve
    /// el timeline COMPLETO (el del 1T con el 2T agregado atrás).
    /// </summary>
    public MatchTimeline SimularSegundoTiempo()
    {
        var tl = LastTimeline!;
        var opp = _rival!;
        var home = EffectiveStarters;

        var full = MatchSimulator.Simulate(
            State.ClubName, "⚽", Power, home,
            opp.Name, opp.Flag, TeamPower.Flat(opp.Strength), _awaySquad,
            knockout: _knockout, seed: _rng.Next(),
            homeRoles: State.Roles,
            half: MatchHalf.Second, resume: tl.Resume);

        LastMatch = full.Result;
        LastTimeline = full;
        LastHomeStarters = home;
        return full;
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
        LastGateM = CobrarEntrada();   // la gente que entró al estadio

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
        LastGateM = CobrarEntrada();   // la gente que entró al estadio

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
