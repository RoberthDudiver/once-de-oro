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

        // Fuerza del equipo con algo de azar de temporada.
        double rating = club.Strength + Rng.Next(-6, 7);
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

        dt.Timeline.Insert(0, new DtSeason
        {
            Year = dt.Year, Club = club.Name, Pos = pos, Teams = teams,
            Played = games, Won = won, Drawn = drawn, Lost = lost, GF = gf, GA = ga,
            Titles = titles, ObjectiveMet = objMet, RepAfter = dt.Rep, Note = note,
        });

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
        else { dt.Objectives = ObjectivesFor(dt.Club!); dt.Pending = null; } // stay: nuevos objetivos
        dt.Year++;
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
