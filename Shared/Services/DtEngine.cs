using OnceDeOro.Data;
using OnceDeOro.Models;

namespace OnceDeOro.Services;

/// <summary>
/// El Modo DT: crea un entrenador, lo contrata un club y simula su carrera
/// temporada a temporada. La directiva pone objetivos y mide la confianza; los
/// resultados suben o bajan la reputación, abren ofertas de clubes más grandes
/// o terminan en un despido.
/// </summary>
public static class DtEngine
{
    private static readonly Random Rng = new();

    // ---- Reputación / confianza ----
    public static string RepName(int rep) =>
        rep >= 80 ? "Leyenda" : rep >= 60 ? "Prestigioso" : rep >= 40 ? "Reconocido" : rep >= 20 ? "Prometedor" : "Desconocido";

    public static int MaxLevel(int rep) => rep >= 72 ? 4 : rep >= 48 ? 3 : rep >= 22 ? 2 : 1;

    public static string ConfName(int c) =>
        c >= 80 ? "Excelente" : c >= 60 ? "Buena" : c >= 40 ? "Aceptable" : c >= 20 ? "En riesgo" : "Muy baja";

    // ---- Alta ----
    public static DtManager NewCareer(string name, string surname, string nationName, int age, DtLicense lic, DtBackground bg)
    {
        var nat = CareerData.NationByName(nationName);
        var dt = new DtManager
        {
            Name = (name ?? "").Trim(),
            Surname = string.IsNullOrWhiteSpace(surname) ? "Míster" : surname.Trim(),
            Nation = nat.Name, NationCode = nat.Code,
            Age = Math.Clamp(age, 28, 65),
            License = lic, Background = bg,
            Rep = lic switch { DtLicense.Profesional => 46, DtLicense.Avanzada => 26, _ => 8 },
            Confidence = 62,
            Started = true, Employed = true,
        };
        // Primer club: el mejor nivel que habilita la licencia.
        int lvl = lic switch { DtLicense.Profesional => 3, DtLicense.Avanzada => 2, _ => 1 };
        Hire(dt, PickClub(dt, lvl, lvl));
        return dt;
    }

    private static DtClub PickClub(DtManager dt, int minLvl, int maxLvl)
    {
        var pool = Enumerable.Range(minLvl, maxLvl - minLvl + 1)
            .SelectMany(DtData.ByLevel)
            .Where(c => c.Name != dt.Club?.Name)
            .OrderBy(_ => Rng.Next()).ToList();
        return pool.FirstOrDefault() ?? DtData.Clubs[0];
    }

    private static void Hire(DtManager dt, DtClub club)
    {
        dt.Club = club;
        dt.Confidence = 62;
        dt.Warned = false;
        dt.Employed = true;
        if (!dt.ClubsManaged.Contains(club.Name)) dt.ClubsManaged.Add(club.Name);
        dt.Objectives = ObjectivesFor(club);
        dt.Pending = null;

        // Economía del nuevo club: cada club llega con su caja, instalaciones e
        // historia. Las mejoras que hiciste en el club anterior quedan allá.
        dt.CajaM = club.BudgetM;
        dt.WageBudgetM = WageBase(club.Level);
        dt.LoanRemainingM = 0; dt.LoanSeasonsLeft = 0;
        dt.TrainCenter = dt.Youth = dt.Medical = dt.Scouting = Math.Clamp(club.Level, 1, 4);
        dt.LastIncomeM = dt.LastExpenseM = 0;
        NewBudget(dt);
    }

    // ---------------------------------------------------------------- economía
    private static int LevelCap(int lvl) => lvl switch { 1 => 68, 2 => 79, 3 => 87, _ => 93 };
    private static int WageBase(int lvl) => lvl switch { 1 => 10, 2 => 32, 3 => 78, _ => 170 };
    private static int IncomeBase(int lvl) => lvl switch { 1 => 24, 2 => 66, 3 => 150, _ => 320 };

    /// <summary>La directiva fija el presupuesto de fichajes de la temporada según nivel, confianza y caja.</summary>
    public static void NewBudget(DtManager dt)
    {
        var c = dt.Club!;
        double conf = 0.6 + dt.Confidence / 100.0 * 0.7;   // 0.6 .. 1.3
        dt.TransferBudgetM = Math.Max(2, (int)Math.Round(c.BudgetM * conf) + Math.Max(0, dt.CajaM) / 6);
        dt.SpentThisSeasonM = 0;
        dt.ScoutPool.Clear();
    }

    /// <summary>Gasta del presupuesto de fichajes para reforzar: sube la fuerza del club (con tope y rendimiento decreciente).</summary>
    public static void Reinforce(DtManager dt, int m)
    {
        var c = dt.Club!;
        m = Math.Clamp(m, 0, dt.TransferBudgetM);
        if (m <= 0) return;
        // Costo por punto según nivel; el scouting lo abarata.
        double costPerPoint = c.Level switch { 1 => 4.0, 2 => 9.0, 3 => 20.0, _ => 42.0 } * (1.0 - (dt.Scouting - 1) * 0.08);
        int gain = (int)Math.Floor(m / Math.Max(1.0, costPerPoint));
        int cap = LevelCap(c.Level);
        c.Strength = Math.Min(cap, c.Strength + gain);
        dt.TransferBudgetM -= m;
        dt.SpentThisSeasonM += m;
    }

    public static int FacilityLevel(DtManager dt, string which) => which switch
    {
        "train" => dt.TrainCenter, "youth" => dt.Youth, "medical" => dt.Medical, _ => dt.Scouting
    };

    /// <summary>Costo (M) de subir una instalación al siguiente nivel.</summary>
    public static int FacilityCost(int currentLevel) => currentLevel switch { 1 => 15, 2 => 30, 3 => 55, 4 => 90, _ => 0 };

    public static bool UpgradeFacility(DtManager dt, string which)
    {
        int lvl = FacilityLevel(dt, which);
        if (lvl >= 5) return false;
        int cost = FacilityCost(lvl);
        if (dt.CajaM < cost) return false;
        dt.CajaM -= cost;
        switch (which)
        {
            case "train": dt.TrainCenter++; break;
            case "youth": dt.Youth++; break;
            case "medical": dt.Medical++; break;
            default: dt.Scouting++; break;
        }
        return true;
    }

    /// <summary>Pide un préstamo: entra a la caja ahora y se paga en 4 temporadas con interés.</summary>
    public static void RequestLoan(DtManager dt, int m)
    {
        m = Math.Clamp(m, 0, 300);
        if (m <= 0) return;
        dt.CajaM += m;
        dt.LoanRemainingM += (int)Math.Round(m * 1.15);
        dt.LoanSeasonsLeft = 4;
    }

    // ---------------------------------------------------------------- mercado / scouting
    private static int ProspectValue(int media, int age)
    {
        double b = Math.Pow(Math.Max(0, media - 45) / 10.0, 3.1) * 2.2;   // M
        double af = age <= 21 ? 1.25 : age <= 27 ? 1.1 : Math.Max(0.2, 1.1 - (age - 27) * 0.12);
        return Math.Max(1, (int)Math.Round(b * af));
    }

    /// <summary>El ojeador busca jugadores. Mejor scouting = más candidatos y de mayor techo.</summary>
    public static void Scout(DtManager dt, Position? pos, int minMedia)
    {
        var club = dt.Club!;
        int n = 3 + dt.Scouting;               // 4..8 candidatos
        int techo = club.Strength + 2 + dt.Scouting * 2;   // el scouting sube el techo del hallazgo
        var list = new List<DtProspect>();
        for (int i = 0; i < n; i++)
        {
            var pp = pos ?? (Position)Rng.Next(0, 4);
            int age = Rng.Next(17, 34);
            int media = Math.Clamp(club.Strength + Rng.Next(-8, 9), 45, Math.Min(96, techo));
            if (media < minMedia) media = Math.Min(techo, minMedia + Rng.Next(0, 4));
            bool free = Rng.NextDouble() < 0.16;
            int pot = age <= 22 ? Math.Min(97, media + Rng.Next(3, 12)) : media + Rng.Next(0, 3);
            var nat = CareerData.Nations[Rng.Next(CareerData.Nations.Count)];
            string name = $"{DtData.FirstNames[Rng.Next(DtData.FirstNames.Length)]} {DtData.LastNames[Rng.Next(DtData.LastNames.Length)]}";
            list.Add(new DtProspect
            {
                Id = "p" + Guid.NewGuid().ToString("N")[..6],
                Name = name, Nation = nat.Name, NationCode = nat.Code, Pos = pp,
                Age = age, Media = media, Potential = pot,
                ValueM = free ? 0 : ProspectValue(media, age),
                Free = free, Club = free ? "Libre" : DtData.Clubs[Rng.Next(DtData.Clubs.Count)].Name,
            });
        }
        dt.ScoutPool = list.OrderByDescending(p => p.Media).ToList();
    }

    /// <summary>Ofertar por un jugador. Devuelve el mensaje del resultado de la negociación.</summary>
    public static string Sign(DtManager dt, string prospectId, int offerM)
    {
        var p = dt.ScoutPool.FirstOrDefault(x => x.Id == prospectId);
        if (p is null) return "";
        var club = dt.Club!;

        if (!p.Free)
        {
            if (dt.TransferBudgetM < offerM) return "No te alcanza el presupuesto de fichajes.";
            if (offerM < p.ValueM) return $"Rechazada: {p.Club} pide ${p.ValueM}M.";
            dt.TransferBudgetM -= offerM;
        }
        // Cuánto aporta al equipo: un crack sube la fuerza; alguien de tu nivel, poco.
        int gain = Math.Clamp(p.Media - club.Strength, 0, 3);
        if (gain == 0 && p.Media >= club.Strength - 1) gain = 1;   // profundidad
        club.Strength = Math.Min(LevelCap(club.Level), club.Strength + gain);

        dt.Signings.Insert(0, $"{p.Name} · {p.Pos} {p.Media}{(p.Free ? " (libre)" : "")}");
        if (dt.Signings.Count > 12) dt.Signings.RemoveAt(dt.Signings.Count - 1);
        dt.ScoutPool.RemoveAll(x => x.Id == p.Id);
        return $"✔ Fichaste a {p.Name} · fuerza +{gain}";
    }

    private static List<DtObjective> ObjectivesFor(DtClub c) => c.Level switch
    {
        1 => new() {
            new() { Text = "No descender", Kind = "nodesc" },
            new() { Text = "Terminar en la mitad de la tabla", Kind = "media" },
        },
        2 => new() {
            new() { Text = "Clasificar a una competición internacional (top 4)", Kind = "intl" },
            new() { Text = "Llegar a la final de una copa", Kind = "copa" },
        },
        3 => new() {
            new() { Text = "Pelear la liga (top 2)", Kind = "media" },
            new() { Text = "Ganar un título", Kind = "copa" },
        },
        _ => new() {
            new() { Text = "Ganar la liga", Kind = "liga" },
            new() { Text = "Ganar un torneo internacional", Kind = "intl" },
        },
    };

    // ---- Jugar la temporada ----
    public static void PlaySeason(DtManager dt)
    {
        if (!dt.Employed || dt.Club is null) return;
        var club = dt.Club;
        int teams = 18, games = (teams - 1) * 2;

        // Desarrollo por instalaciones: centro de entrenamiento + cantera hacen
        // crecer la fuerza del club temporada a temporada (hasta el tope del nivel).
        int dev = dt.TrainCenter + dt.Youth;   // 2..10
        if (dev >= 4 && club.Strength < LevelCap(club.Level))
            club.Strength = Math.Min(LevelCap(club.Level), club.Strength + (dev >= 8 ? 2 : 1));

        // Fuerza del equipo con algo de azar de temporada. El departamento médico
        // recorta la mala suerte (menos lesiones/imponderables en contra).
        double rating = club.Strength + Rng.Next(-6 + (dt.Medical - 1), 7);
        // Campo de rivales alrededor del nivel del club.
        int baseRival = club.Level switch { 1 => 60, 2 => 71, 3 => 80, _ => 86 };
        var field = Enumerable.Range(0, teams - 1)
            .Select(_ => baseRival + Rng.Next(-8, 9)).ToList();
        int pos = 1 + field.Count(r => r > rating);

        // Resultados coherentes con la posición y la fuerza.
        double winRate = Math.Clamp(0.30 + (rating - field.Average()) / 42.0 + (teams / 2.0 - pos) / teams * 0.5, 0.10, 0.85);
        double lossRate = Math.Clamp(0.55 - winRate, 0.05, 0.55);
        int won = (int)Math.Round(games * winRate);
        int lost = (int)Math.Round(games * lossRate);
        int drawn = Math.Max(0, games - won - lost);
        int gf = (int)Math.Round(won * 2.4 + drawn * 1.0 + lost * 0.6);
        int ga = (int)Math.Round(lost * 2.2 + drawn * 1.0 + won * 0.5);

        var titles = new List<string>();
        bool champ = pos == 1;
        if (champ) titles.Add("🏆 Liga");
        // Copa nacional: chance según fuerza.
        if (Rng.NextDouble() < 0.10 + (rating - field.Average()) / 120.0) titles.Add("🏅 Copa");
        // Internacional: solo clubes grandes/gigantes que anduvieron bien.
        if (club.Level >= 3 && pos <= 2 && Rng.NextDouble() < (club.Level == 4 ? 0.35 : 0.18))
            titles.Add("⭐ Internacional");

        // ¿Se cumplieron los objetivos?
        bool relegated = pos >= teams - 1;
        int mediaCut = (int)Math.Ceiling(teams * 0.6);   // "mitad de tabla" = top 60% (no tan exigente)
        foreach (var o in dt.Objectives)
            o.Met = o.Kind switch
            {
                "nodesc" => !relegated,
                "media" => o.Text.Contains("top 2") ? pos <= 2 : pos <= mediaCut,
                "intl" => titles.Contains("⭐ Internacional") || pos <= 4,
                "copa" => titles.Any(),
                "liga" => champ,
                _ => pos <= mediaCut,
            };
        bool objMet = dt.Objectives.All(o => o.Met);

        // Confianza y reputación.
        int dConf = (objMet ? 16 : -22) + (champ ? 12 : 0) + titles.Count * 6 + (relegated ? -25 : 0);
        dt.Confidence = Math.Clamp(dt.Confidence + dConf, 0, 100);
        int dRep = titles.Count * 10 + (objMet ? 8 : -3)
                 + (titles.Contains("⭐ Internacional") ? 10 : 0)
                 + (pos <= 3 ? 5 : 0) + (champ ? 4 : 0) - (relegated ? 6 : 0);
        dt.Rep = Math.Clamp(dt.Rep + dRep, 0, 100);

        // Acumulados.
        dt.Matches += games; dt.Wins += won; dt.Draws += drawn; dt.Losses += lost;
        dt.Titles += titles.Count; dt.SeasonsManaged++;

        string note = titles.Count > 0 ? string.Join(" · ", titles)
                    : objMet ? "✔ Objetivos cumplidos"
                    : relegated ? "⬇ Descenso" : "✖ Objetivos incumplidos";

        // ---- Finanzas de la temporada ----
        int upkeep = (dt.TrainCenter + dt.Youth + dt.Medical + dt.Scouting) * 3;   // mantenimiento
        int loanPay = dt.LoanSeasonsLeft > 0 ? (int)Math.Ceiling(dt.LoanRemainingM / (double)dt.LoanSeasonsLeft) : 0;
        if (loanPay > 0) { dt.LoanRemainingM -= loanPay; dt.LoanSeasonsLeft--; }
        int income = IncomeBase(club.Level)
                   + Math.Max(0, (teams / 2 - pos)) * 3            // mejor puesto, más ingresos
                   + titles.Count * 25
                   + (titles.Contains("⭐ Internacional") ? 60 : 0);
        int expense = WageBase(club.Level) + upkeep + loanPay;
        dt.CajaM += income - expense;
        dt.LastIncomeM = income;
        dt.LastExpenseM = expense;

        dt.Timeline.Insert(0, new DtSeason
        {
            Year = dt.Year, Club = club.Name, Pos = pos, Teams = teams,
            Played = games, Won = won, Drawn = drawn, Lost = lost, GF = gf, GA = ga,
            Titles = titles, ObjectiveMet = objMet, RepAfter = dt.Rep, Note = note,
        });

        WorldAndNews(dt, pos, titles, champ);
        dt.Pending = PostSeason(dt, objMet, relegated);
    }

    // ---- Fin de temporada: renovar, ofertas o despido ----
    private static DtDecision PostSeason(DtManager dt, bool objMet, bool relegated)
    {
        var club = dt.Club!;

        // ¿Despido? Confianza crítica y sin cumplir.
        if (dt.Confidence < 22 && !objMet)
        {
            if (dt.Warned || relegated)
            {
                dt.Employed = false;
                dt.TimesFired++;
                dt.Rep = Math.Clamp(dt.Rep - 8, 0, 100);
                return NewJobDecision(dt, "🚪 Te echaron",
                    $"La directiva de {club.Name} decidió terminar tu ciclo. Buscá un nuevo proyecto.");
            }
            dt.Warned = true;
        }
        else dt.Warned = false;

        // Sigue con trabajo: renovar u ofertas de clubes más grandes.
        var d = new DtDecision
        {
            Title = objMet ? "Fin de temporada" : "Temporada difícil",
            Text = objMet
                ? "La directiva está conforme. Podés seguir el proyecto o escuchar ofertas."
                : "Los números no fueron los esperados. Decidí cómo seguir.",
        };
        d.Options.Add(new DtOption { Label = $"Seguir en {club.Name}", Sub = "renovar el proyecto", Kind = "stay", Club = club });

        // Ofertas: hasta el nivel que habilita la reputación, mejores que el actual.
        int max = MaxLevel(dt.Rep);
        if (max > club.Level || (max == club.Level && dt.Rep >= 40))
        {
            var offers = Enumerable.Range(club.Level, max - club.Level + 1)
                .SelectMany(DtData.ByLevel)
                .Where(c => c.Name != club.Name && c.Level >= club.Level)
                .OrderBy(_ => Rng.Next()).Take(2).ToList();
            foreach (var c in offers)
                d.Options.Add(new DtOption { Label = $"Firmar por {c.Name}", Sub = $"{DtData.LevelName(c.Level)} · {c.League}", Kind = "move", Club = c });
        }
        return d;
    }

    private static DtDecision NewJobDecision(DtManager dt, string title, string text)
    {
        var d = new DtDecision { Title = title, Text = text };
        int max = MaxLevel(dt.Rep);
        // Sin trabajo se ofrecen clubes hasta tu nivel (a veces uno menos).
        var pool = Enumerable.Range(1, max)
            .SelectMany(DtData.ByLevel)
            .Where(c => c.Name != dt.Club?.Name)
            .OrderBy(_ => Rng.Next()).Take(3).ToList();
        foreach (var c in pool)
            d.Options.Add(new DtOption { Label = $"Firmar por {c.Name}", Sub = $"{DtData.LevelName(c.Level)} · {c.League}", Kind = "move", Club = c });
        return d;
    }

    // ---- Aplicar una decisión ----
    public static void Choose(DtManager dt, DtOption opt)
    {
        if (opt.Kind == "move" && opt.Club is not null) Hire(dt, opt.Club);
        else { dt.Objectives = ObjectivesFor(dt.Club!); dt.Pending = null; NewBudget(dt); } // stay: nuevos objetivos + presupuesto
        dt.Year++;
    }

    // ---------------------------------------------------------------- mundo vivo
    private static string RandName() =>
        $"{DtData.FirstNames[Rng.Next(DtData.FirstNames.Length)]} {DtData.LastNames[Rng.Next(DtData.LastNames.Length)]}";

    /// <summary>Genera premios, noticias del mundo e historial al cerrar la temporada.</summary>
    private static void WorldAndNews(DtManager dt, int pos, List<string> titles, bool champ)
    {
        var club = dt.Club!;
        dt.News.Clear();

        // Tu resumen.
        dt.News.Add(new DtNews { Icon = "📋", Text = $"{club.Name} terminó {pos}º" + (titles.Count > 0 ? $" y ganó {string.Join(", ", titles)}" : "") });

        // Campeones de otras grandes ligas.
        var gigs = DtData.ByLevel(4).Where(c => c.Name != club.Name).OrderBy(_ => Rng.Next()).Take(3).ToList();
        foreach (var g in gigs) dt.News.Add(new DtNews { Icon = "🏆", Text = $"{g.Name} se coronó campeón de {g.League}" });

        // Fichaje bomba y joven promesa.
        var buyer = DtData.ByLevel(4).OrderBy(_ => Rng.Next()).First();
        dt.News.Add(new DtNews { Icon = "💸", Text = $"{buyer.Name} fichó a {RandName()} por ${Rng.Next(70, 190)}M" });
        dt.News.Add(new DtNews { Icon = "🌟", Text = $"{RandName()} ({Rng.Next(17, 20)} años) es la sensación de la temporada" });
        dt.News.Add(new DtNews { Icon = "🔁", Text = $"{DtData.Clubs[Rng.Next(DtData.Clubs.Count)].Name} cambió de entrenador" });

        // Premios individuales.
        string ballon = (champ && dt.Signings.Count > 0 && Rng.NextDouble() < 0.45)
            ? dt.Signings[0].Split(" ·")[0]        // uno de tus cracks
            : RandName();
        string bota = RandName();

        bool youWin = (champ || titles.Contains("⭐ Internacional")) && Rng.NextDouble() < (club.Level >= 4 ? 0.55 : 0.30);
        string dtAward;
        if (youWin)
        {
            dt.Awards.Insert(0, $"🏅 DT del Año · T{dt.Year}");
            dt.Rep = Math.Clamp(dt.Rep + 5, 0, 100);
            dtAward = $"{dt.Surname} — 🏅 DT del Año";
        }
        else dtAward = $"{RandName()} — DT del Año";

        dt.News.Add(new DtNews { Icon = "🥇", Text = $"Balón de Oro: {ballon} · Bota de Oro: {bota}" });
        dt.News.Add(new DtNews { Icon = "🎖️", Text = dtAward });

        // Historial persistente del mundo.
        dt.WorldLog.Insert(0, $"T{dt.Year}: 🏆 {gigs.FirstOrDefault()?.Name ?? club.Name} · 🥇 {ballon}");
        if (dt.WorldLog.Count > 24) dt.WorldLog.RemoveAt(dt.WorldLog.Count - 1);
    }

    // ---- Etiqueta de legado ----
    public static string Legacy(DtManager dt)
    {
        if (dt.Rep >= 80 && dt.Titles >= 12) return "🐐 Leyenda de los banquillos";
        if (dt.Rep >= 60 && dt.Titles >= 6) return "👑 DT de elite";
        if (dt.Rep >= 40) return "⭐ Entrenador reconocido";
        if (dt.Rep >= 20) return "🎯 Técnico en ascenso";
        return "🌱 Empezando el camino";
    }
}
