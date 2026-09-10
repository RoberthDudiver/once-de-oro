using OnceDeOro.Data;
using OnceDeOro.Models;

namespace OnceDeOro.Services;

/// <summary>
/// El Modo DT: crea un entrenador, lo contrata un club y dirige su carrera. La
/// temporada se juega FECHA A FECHA (calendario real), con plantilla propia,
/// tabla de posiciones, formación y XI. Al terminar el calendario, la directiva
/// evalúa (confianza/reputación), pasa el mercado y llegan ofertas o el despido.
/// </summary>
public static class DtEngine
{
    private static readonly Random Rng = new();
    private const int Teams = 14;                 // vos + 13 rivales
    public const int MaxCareerSeasons = 15;
    private static int Rounds => (Teams - 1) * 2; // ida y vuelta = 26 fechas

    // ---- Reputación / confianza ----
    public static string RepName(int rep) =>
        rep >= 80 ? "Leyenda" : rep >= 60 ? "Prestigioso" : rep >= 40 ? "Reconocido" : rep >= 20 ? "Prometedor" : "Desconocido";

    public static int MaxLevel(int rep) => rep >= 72 ? 4 : rep >= 48 ? 3 : rep >= 22 ? 2 : 1;

    public static string ConfName(int c) =>
        c >= 80 ? "Excelente" : c >= 60 ? "Buena" : c >= 40 ? "Aceptable" : c >= 20 ? "En riesgo" : "Muy baja";

    // ---- Alta ----
    public static DtManager NewCareer(string name, string surname, string nationName, int age, DtLicense lic, DtBackground bg, List<string>? languages = null)
    {
        var nat = CareerData.NationByName(nationName);
        var dt = new DtManager
        {
            Name = (name ?? "").Trim(),
            Surname = string.IsNullOrWhiteSpace(surname) ? "Míster" : surname.Trim(),
            Nation = nat.Name, NationCode = nat.Code,
            Age = Math.Clamp(age, 28, 65),
            License = lic, Background = bg,
            Languages = languages is { Count: > 0 } ? languages : new() { "Español" },
            Rep = lic switch { DtLicense.Profesional => 46, DtLicense.Avanzada => 26, _ => 8 },
            Confidence = 62,
            Started = true, Employed = true,
        };
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
        // Copiamos el club (fuerza/caja son estado de TU tenencia, no del club global).
        dt.Club = new DtClub { Name = club.Name, Emblem = club.Emblem, Country = club.Country, League = club.League, Level = club.Level, Strength = club.Strength, BudgetM = club.BudgetM };
        dt.Confidence = 62;
        dt.Warned = false;
        dt.Employed = true;
        if (!dt.ClubsManaged.Contains(club.Name)) dt.ClubsManaged.Add(club.Name);

        dt.CajaM = club.BudgetM;
        dt.WageBudgetM = WageBase(club.Level);
        dt.LoanRemainingM = 0; dt.LoanSeasonsLeft = 0;
        dt.TrainCenter = dt.Youth = dt.Medical = dt.Scouting = Math.Clamp(club.Level, 1, 4);
        dt.LastIncomeM = dt.LastExpenseM = 0;

        BuildSquad(dt);
        StartSeason(dt);
    }

    // ---------------------------------------------------------------- plantilla
    private static int PlayerSalary(int media) => Math.Max(1, (int)Math.Round(Math.Pow(Math.Max(0, media - 45) / 10.0, 2.6)));
    private static int PlayerValue(int media, int age)
    {
        double b = Math.Pow(Math.Max(0, media - 45) / 10.0, 3.1) * 2.2;
        double af = age <= 21 ? 1.25 : age <= 27 ? 1.1 : Math.Max(0.2, 1.1 - (age - 27) * 0.12);
        return Math.Max(1, (int)Math.Round(b * af));
    }

    private static DtPlayer GenPlayer(int baseMedia, Position pos)
    {
        int media = Math.Clamp(baseMedia + Rng.Next(-9, 6), 42, 96);
        int age = Rng.Next(17, 35);
        int pot = age <= 22 ? Math.Min(97, media + Rng.Next(2, 12)) : media + Rng.Next(0, 3);
        var nat = CareerData.Nations[Rng.Next(CareerData.Nations.Count)];
        return new DtPlayer
        {
            Id = "sq" + Guid.NewGuid().ToString("N")[..7],
            Name = $"{DtData.FirstNames[Rng.Next(DtData.FirstNames.Length)]} {DtData.LastNames[Rng.Next(DtData.LastNames.Length)]}",
            Nation = nat.Name, NationCode = nat.Code, Pos = pos,
            Age = age, Media = media, Potential = pot,
            Morale = Rng.Next(60, 82), Fatigue = 0, InjuryWeeks = 0,
            ContractYears = Rng.Next(1, 5),
            SalaryM = PlayerSalary(media), ValueM = PlayerValue(media, age),
        };
    }

    private static void BuildSquad(DtManager dt)
    {
        int b = dt.Club!.Strength + 2;   // el mejor ronda la fuerza del club
        var sq = new List<DtPlayer>();
        void Add(Position p, int n) { for (int i = 0; i < n; i++) sq.Add(GenPlayer(b, p)); }
        Add(Position.GK, 3); Add(Position.DEF, 7); Add(Position.MID, 7); Add(Position.FWD, 5);
        dt.Squad = sq.OrderByDescending(p => p.Media).ToList();
        dt.Lineup.Clear();
        AutoLineup(dt);
        RecalcStrength(dt);
    }

    /// <summary>Recalcula la fuerza del club como el promedio del mejor XI.</summary>
    public static void RecalcStrength(DtManager dt)
    {
        var xi = BestXI(dt);
        if (xi.Count > 0) dt.Club!.Strength = (int)Math.Round(xi.Average(p => p.Media));
    }

    private static (int d, int m, int f) Slots(string form) => form switch
    {
        "4-4-2" => (4, 4, 2), "3-5-2" => (3, 5, 2), "5-3-2" => (5, 3, 2),
        "4-2-3-1" => (4, 5, 1), "4-5-1" => (4, 5, 1), _ => (4, 3, 3),
    };

    public static readonly string[] Formations = { "4-3-3", "4-4-2", "4-2-3-1", "3-5-2", "5-3-2" };

    /// <summary>El once que juega: tu alineación elegida si es válida, si no la automática.</summary>
    public static List<DtPlayer> BestXI(DtManager dt)
    {
        var avail = dt.Squad.Where(p => p.InjuryWeeks <= 0).ToList();
        var chosen = dt.Lineup.Select(id => avail.FirstOrDefault(p => p.Id == id)).Where(p => p is not null).Cast<DtPlayer>().ToList();
        if (chosen.Count == 11 && chosen.Any(p => p.Pos == Position.GK)) return chosen;
        return AutoXI(dt, avail);
    }

    private static List<DtPlayer> AutoXI(DtManager dt, List<DtPlayer> avail)
    {
        var (d, m, f) = Slots(dt.Formation);
        var xi = new List<DtPlayer>();
        List<DtPlayer> Best(Position pos, int n) => avail.Where(p => p.Pos == pos && !xi.Contains(p)).OrderByDescending(p => p.Media).Take(n).ToList();
        xi.AddRange(Best(Position.GK, 1));
        xi.AddRange(Best(Position.DEF, d));
        xi.AddRange(Best(Position.MID, m));
        xi.AddRange(Best(Position.FWD, f));
        // Si faltan por lesiones, completar con lo mejor disponible.
        foreach (var p in avail.OrderByDescending(p => p.Media))
        {
            if (xi.Count >= 11) break;
            if (!xi.Contains(p)) xi.Add(p);
        }
        return xi;
    }

    public static void AutoLineup(DtManager dt)
    {
        var xi = AutoXI(dt, dt.Squad.Where(p => p.InjuryWeeks <= 0).ToList());
        dt.Lineup = xi.Select(p => p.Id).ToList();
    }

    public static void ToggleStarter(DtManager dt, string id)
    {
        if (dt.Lineup.Contains(id)) dt.Lineup.Remove(id);
        else if (dt.Lineup.Count < 11) dt.Lineup.Add(id);
    }

    public static void SetFormation(DtManager dt, string form) { dt.Formation = form; AutoLineup(dt); }

    public static void SetTactics(DtManager dt, string mentality, string tempo, string press, string build, string line)
    { dt.Mentality = mentality; dt.Tempo = tempo; dt.Press = press; dt.Build = build; dt.Line = line; }

    /// <summary>Nombre del nivel de lesión según las semanas de baja (como el PDF).</summary>
    public static string InjuryLevel(int weeks) => weeks <= 0 ? "" : weeks <= 1 ? "Molestia" : weeks <= 3 ? "Leve" : weeks <= 6 ? "Moderada" : weeks <= 12 ? "Grave" : "Muy grave";

    /// <summary>Vende un jugador: entra su valor a la caja y sale del plantel.</summary>
    public static string SellPlayer(DtManager dt, string id)
    {
        if (dt.Squad.Count <= 14) return "No podés bajar de 14 jugadores.";
        var p = dt.Squad.FirstOrDefault(x => x.Id == id);
        if (p is null) return "";
        dt.CajaM += p.ValueM;
        dt.Squad.Remove(p);
        dt.Lineup.Remove(p.Id);
        AutoLineup(dt); RecalcStrength(dt);
        return $"💰 Vendiste a {p.Name} por ${p.ValueM}M";
    }

    /// <summary>Renueva el contrato de un jugador: cuesta un sueldo y suma años + moral.</summary>
    public static string RenewPlayer(DtManager dt, string id)
    {
        var p = dt.Squad.FirstOrDefault(x => x.Id == id);
        if (p is null) return "";
        if (dt.CajaM < p.SalaryM) return "No te alcanza la caja para la prima.";
        dt.CajaM -= p.SalaryM;
        p.ContractYears += 2;
        p.Morale = Math.Clamp(p.Morale + 6, 20, 100);
        return $"🖊️ Renovaste a {p.Name} (+2 años)";
    }

    // ---------------------------------------------------------------- calendario
    private static void StartSeason(DtManager dt)
    {
        var club = dt.Club!;
        // Resetear estadísticas de temporada de la plantilla y curar lesiones.
        foreach (var p in dt.Squad) { p.Apps = p.Goals = p.Assists = 0; p.Fatigue = 0; p.InjuryWeeks = 0; p.Morale = Math.Clamp(p.Morale, 62, 78); }

        EnsureWorld(dt);
        dt.WorldClub[club.Name] = club.Strength;   // el mundo conoce tu club
        int baseRival = club.Level switch { 1 => 60, 2 => 71, 3 => 80, _ => 86 };
        // Rivales: clubes reales del nivel + genéricos hasta llegar a 13.
        var names = DtData.ByLevel(club.Level).Where(c => c.Name != club.Name).Select(c => c.Name).ToList();
        while (names.Count < Teams - 1) names.Add($"{DtData.LastNames[Rng.Next(DtData.LastNames.Length)]} FC");
        names = names.OrderBy(_ => Rng.Next()).Take(Teams - 1).ToList();

        dt.Table = new List<DtTableRow> { new() { Name = club.Name, Strength = club.Strength, IsMe = true } };
        // La fuerza del rival sale de cómo evolucionó ese club en el mundo (o del promedio del nivel si es genérico).
        foreach (var n in names) dt.Table.Add(new DtTableRow { Name = n, Strength = dt.WorldClub.TryGetValue(n, out var ws) ? ws : baseRival + Rng.Next(-8, 9) });

        // Fixture: cada rival dos veces (local y visitante), barajado.
        var fix = new List<DtMatch>();
        foreach (var r in dt.Table.Where(t => !t.IsMe))
        {
            fix.Add(new DtMatch { Opp = r.Name, OppStrength = r.Strength, Home = true });
            fix.Add(new DtMatch { Opp = r.Name, OppStrength = r.Strength, Home = false });
        }
        fix = fix.OrderBy(_ => Rng.Next()).ToList();
        for (int i = 0; i < fix.Count; i++) fix[i].Round = i + 1;
        dt.Fixture = fix;

        dt.Round = 0;
        dt.SeasonInPlay = true;
        dt.Pending = null;
        NewBudget(dt);
        dt.Objectives = ObjectivesFor(club);
        BuildCalendar(dt);
        AutoLineup(dt);
    }

    private static readonly string[] Months =
        { "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre", "Enero", "Febrero", "Marzo", "Abril", "Mayo" };

    /// <summary>Arma el calendario del año: partidos, entrenamientos, prensa y descansos, repartidos por meses.</summary>
    private static void BuildCalendar(DtManager dt)
    {
        int matches = dt.Fixture.Count;
        var seq = new List<string>();
        for (int i = 0; i < matches; i++)
        {
            if (i > 0 && i % 3 == 0) seq.Add("train");
            if (i == 8 || i == 18) seq.Add("press");
            if (i == 13) seq.Add("rest");
            seq.Add("match");
        }
        seq.Add("train");   // semana final de cierre

        dt.Calendar = new List<DtWeek>();
        for (int i = 0; i < seq.Count; i++)
        {
            string month = Months[Math.Min(Months.Length - 1, i * Months.Length / seq.Count)];
            dt.Calendar.Add(new DtWeek { Index = i, Month = month, Type = seq[i] });
        }
        dt.Week = 0;
        dt.PressPending = null;
        dt.WeekMsg = "";
    }

    private static double GoalExp(int a, int b) => Math.Clamp(1.35 * Math.Exp((a - b) / 16.0), 0.2, 4.0);
    private static int Poisson(double l)
    {
        double p = Math.Exp(-l), c = p; double u = Rng.NextDouble(); int k = 0;
        while (u > c && k < 12) { k++; p *= l / k; c += p; }
        return k;
    }

    private static void Record(DtTableRow t, int gf, int ga)
    {
        t.Played++; t.GF += gf; t.GA += ga;
        if (gf > ga) t.Won++; else if (gf == ga) t.Drawn++; else t.Lost++;
    }

    public static int XiStrength(DtManager dt)
    {
        var xi = BestXI(dt);
        if (xi.Count == 0) return dt.Club!.Strength;
        double avg = xi.Average(p => p.Media);
        double mor = 0.94 + xi.Average(p => p.Morale) / 100.0 * 0.12;
        double fat = xi.Average(p => p.Fatigue) / 100.0 * 6.0;
        return (int)Math.Round(avg * mor - fat);
    }

    /// <summary>Juega la próxima fecha del calendario.</summary>
    public static void PlayNextMatch(DtManager dt)
    {
        if (!dt.SeasonInPlay || dt.Round >= dt.Fixture.Count) return;
        var mtch = dt.Fixture[dt.Round];
        int my = XiStrength(dt), opp = mtch.OppStrength;

        // ---- Táctica: mentalidad, ritmo, presión, construcción y línea ----
        int ment = dt.Mentality switch { "ultradef" => -2, "def" => -1, "of" => 1, "ultraof" => 2, _ => 0 };
        int tempo = dt.Tempo switch { "lento" => -1, "rapido" => 1, _ => 0 };
        int pres = dt.Press switch { "baja" => -1, "alta" => 1, _ => 0 };
        int line = dt.Line switch { "baja" => -1, "alta" => 1, _ => 0 };
        double chanceMult = 1 + (tempo + pres) * 0.06 + line * 0.03;   // más ritmo/presión/línea = más goles ambos

        double eMy = GoalExp(my + ment * 2 + line, opp) * (mtch.Home ? 1.12 : 0.9) * chanceMult;
        double eOpp = GoalExp(opp, my - ment + line) * (mtch.Home ? 0.9 : 1.12) * chanceMult;
        if (dt.Build == "posesion") eOpp *= 0.93;                      // controlás la pelota, el rival crea menos
        else if (dt.Build == "directo") eMy *= 1.06;                   // ataque directo, más volumen
        else eMy *= 1.03;                                              // contraataque

        int mg = Poisson(eMy), og = Poisson(eOpp);
        ApplyMatchOutcome(dt, mtch, mg, og);
    }

    /// <summary>Aplica el resultado de un partido (tabla, goleadores, cansancio, lesiones, resto de la fecha).
    /// Lo usan el partido rápido y el partido MIRADO en la cancha 2D.</summary>
    private static void ApplyMatchOutcome(DtManager dt, DtMatch mtch, int mg, int og)
    {
        var xi = BestXI(dt);
        int tempo = dt.Tempo switch { "lento" => -1, "rapido" => 1, _ => 0 };
        int pres = dt.Press switch { "baja" => -1, "alta" => 1, _ => 0 };
        double fatMult = 1 + (tempo + pres) * 0.15;

        mtch.MyGoals = mg; mtch.OppGoals = og; mtch.Played = true;
        var hurt = new List<string>();

        // Tabla: mi fila y la del rival.
        var me = dt.Table.First(t => t.IsMe);
        Record(me, mg, og);
        var oppRow = dt.Table.FirstOrDefault(t => t.Name == mtch.Opp);
        if (oppRow is not null) Record(oppRow, og, mg);

        // Estadísticas de mis jugadores (titulares).
        var att = xi.Where(p => p.Pos == Position.FWD || p.Pos == Position.MID).ToList();
        foreach (var p in xi)
        {
            p.Apps++;
            p.Fatigue = Math.Min(100, p.Fatigue + (int)Math.Round(Rng.Next(9, 17) * fatMult));
            // Lesión (5 niveles): más riesgo con presión/ritmo alto y cansancio; menos con buen médico.
            if (p.Fatigue > 55 && Rng.NextDouble() < 0.02 * fatMult * (1.0 - (dt.Medical - 1) * 0.12))
            {
                int wk = Rng.Next(1, 14);
                p.InjuryWeeks = Math.Max(1, wk - (dt.Medical - 1));
                hurt.Add($"{p.Name} ({InjuryLevel(p.InjuryWeeks)})");
            }
            p.Morale = Math.Clamp(p.Morale + (mg > og ? 4 : mg == og ? 0 : -4), 20, 100);
        }
        for (int g = 0; g < mg && att.Count > 0; g++)
        {
            var scorer = att[Rng.Next(att.Count)]; scorer.Goals++;
            if (att.Count > 1 && Rng.NextDouble() < 0.6) { var a = att[Rng.Next(att.Count)]; if (a != scorer) a.Assists++; }
        }
        // Descanso de los suplentes.
        foreach (var p in dt.Squad.Where(p => !xi.Contains(p)))
        {
            p.Fatigue = Math.Max(0, p.Fatigue - 12);
            if (p.InjuryWeeks > 0) p.InjuryWeeks--;
        }

        // Simular el resto de la fecha (los otros equipos).
        var others = dt.Table.Where(t => !t.IsMe && t.Name != mtch.Opp).OrderBy(_ => Rng.Next()).ToList();
        for (int i = 0; i + 1 < others.Count; i += 2)
        {
            int ga = Poisson(GoalExp(others[i].Strength, others[i + 1].Strength));
            int gb = Poisson(GoalExp(others[i + 1].Strength, others[i].Strength));
            Record(others[i], ga, gb); Record(others[i + 1], gb, ga);
        }

        string res = mtch.Home ? $"{dt.Club!.Name} {mg}-{og} {mtch.Opp}" : $"{mtch.Opp} {og}-{mg} {dt.Club!.Name}";
        dt.WeekMsg = "⚽ " + res + (hurt.Count > 0 ? " · 🤕 " + string.Join(", ", hurt) : "");

        dt.Round++;
        // El fin de temporada lo decide el CALENDARIO (cuando pasan todos los meses),
        // no el último partido: pueden quedar semanas de entrenamiento/prensa.
    }

    /// <summary>Datos para VER el partido en la cancha 2D (como el modo principal): XI propio, XI rival y fuerzas.</summary>
    public static (List<Player> home, List<Player> away, int homeStr, int awayStr, string oppName)? MatchViewData(DtManager dt)
    {
        if (!dt.SeasonInPlay || dt.Round >= dt.Fixture.Count || dt.Week >= dt.Calendar.Count || dt.Calendar[dt.Week].Type != "match") return null;
        var mtch = dt.Fixture[dt.Round];
        var xi = BestXI(dt);
        var home = xi.Select((p, i) => new Player { Id = $"h{i}", Name = p.Name, Nation = p.Nation, Flag = "⚽", Pos = p.Pos, Rating = Math.Clamp(p.Media, 40, 99) }).ToList();
        var away = GenAwayXI(mtch.Opp, mtch.OppStrength);
        return (home, away, XiStrength(dt), mtch.OppStrength, mtch.Opp);
    }

    private static List<Player> GenAwayXI(string name, int str)
    {
        var poss = new[] { Position.GK, Position.DEF, Position.DEF, Position.DEF, Position.DEF, Position.MID, Position.MID, Position.MID, Position.FWD, Position.FWD, Position.FWD };
        var list = new List<Player>();
        for (int i = 0; i < 11; i++)
            list.Add(new Player { Id = $"a{i}", Name = DtData.LastNames[(i * 3 + str) % DtData.LastNames.Length], Nation = name, Flag = "🔴", Pos = poss[i], Rating = Math.Clamp(str + (i % 3 - 1) * 2, 40, 99) });
        return list;
    }

    /// <summary>Cierra un partido MIRADO en la cancha 2D: aplica el resultado del timeline y avanza la semana.</summary>
    public static void FinishWatchedMatch(DtManager dt, int myGoals, int oppGoals)
    {
        if (!dt.SeasonInPlay || dt.Round >= dt.Fixture.Count || dt.Week >= dt.Calendar.Count || dt.Calendar[dt.Week].Type != "match") return;
        var mtch = dt.Fixture[dt.Round];
        ApplyMatchOutcome(dt, mtch, myGoals, oppGoals);
        dt.Calendar[dt.Week].Done = true;
        dt.Week++;
        if (dt.Week >= dt.Calendar.Count) EndSeason(dt);
    }

    // ---------------------------------------------------------------- calendario: avanzar el tiempo
    public static string PlanName(string p) => p switch
    {
        "ataque" => "Ataque", "defensa" => "Defensa", "fisico" => "Condición física",
        "balon" => "Balón parado", "juvenil" => "Desarrollo juvenil", "recuperacion" => "Recuperación", _ => "Táctica"
    };

    public static void SetTraining(DtManager dt, string plan, string intensity)
    {
        dt.TrainPlan = plan; dt.TrainIntensity = intensity;
    }

    private static void ApplyTraining(DtManager dt)
    {
        int inten = dt.TrainIntensity switch { "alta" => 3, "baja" => 1, _ => 2 };
        foreach (var p in dt.Squad.Where(p => p.InjuryWeeks <= 0))
        {
            if (dt.TrainPlan == "recuperacion") p.Fatigue = Math.Max(0, p.Fatigue - 14 - inten * 3);
            else if (dt.TrainPlan == "fisico")
            {
                p.Fatigue = Math.Max(0, p.Fatigue - 6);
                if (dt.TrainIntensity == "alta" && Rng.NextDouble() < 0.02) p.InjuryWeeks = Rng.Next(1, 4);
            }
            else
            {
                p.Fatigue = Math.Min(100, p.Fatigue + inten * 2);
                bool joven = dt.TrainPlan == "juvenil" && p.Age <= 22;
                if ((joven && Rng.NextDouble() < 0.10 * inten) || (p.Age <= 24 && Rng.NextDouble() < 0.04 * inten))
                    p.Media = Math.Min(p.Potential, p.Media + 1);
            }
        }
        RecalcStrength(dt);
        dt.WeekMsg = $"🏋️ Entrenamiento: {PlanName(dt.TrainPlan)} · {dt.TrainIntensity}";
    }

    private static DtDecision GenPress(DtManager dt)
    {
        var d = new DtDecision { Title = "🎙️ Rueda de prensa" };
        int topic = Rng.Next(0, 3);
        if (topic == 0)
        {
            d.Text = "Un periodista cuestiona el rendimiento del equipo.";
            d.Options.Add(new DtOption { Label = "Bancar a los jugadores", Sub = "sube la moral", Kind = "press", MoraleDelta = 6, ConfDelta = -2 });
            d.Options.Add(new DtOption { Label = "Exigir más en público", Sub = "presión", Kind = "press", MoraleDelta = -5, ConfDelta = 4 });
            d.Options.Add(new DtOption { Label = "Evitar la polémica", Sub = "neutral", Kind = "press", MoraleDelta = 1, ConfDelta = 0 });
        }
        else if (topic == 1)
        {
            d.Text = "Te preguntan por rumores de una oferta de otro club.";
            d.Options.Add(new DtOption { Label = "Prometer que te quedás", Sub = "gana la directiva", Kind = "press", MoraleDelta = 2, ConfDelta = 5 });
            d.Options.Add(new DtOption { Label = "No cerrar puertas", Sub = "arriesgado", Kind = "press", MoraleDelta = 0, ConfDelta = -5 });
            d.Options.Add(new DtOption { Label = "No responder", Sub = "neutral", Kind = "press", MoraleDelta = 0, ConfDelta = 0 });
        }
        else
        {
            d.Text = "El capitán pidió refuerzos en la prensa.";
            d.Options.Add(new DtOption { Label = "Prometer fichajes", Sub = "sube la moral", Kind = "press", MoraleDelta = 6, ConfDelta = -3 });
            d.Options.Add(new DtOption { Label = "Confiar en el plantel", Sub = "gana la directiva", Kind = "press", MoraleDelta = -2, ConfDelta = 3 });
            d.Options.Add(new DtOption { Label = "Bajarle el tono", Sub = "neutral", Kind = "press", MoraleDelta = 1, ConfDelta = 0 });
        }
        return d;
    }

    public static void AnswerPress(DtManager dt, DtOption opt)
    {
        if (dt.PressPending is null) return;
        foreach (var p in dt.Squad) p.Morale = Math.Clamp(p.Morale + opt.MoraleDelta, 20, 100);
        dt.Confidence = Math.Clamp(dt.Confidence + opt.ConfDelta, 0, 100);
        dt.WeekMsg = $"🎙️ {opt.Label}";
        dt.PressPending = null;
    }

    /// <summary>Procesa la semana actual del calendario (partido, entrenamiento, prensa o descanso).</summary>
    public static void AdvanceWeek(DtManager dt)
    {
        if (dt.CareerFinished || !dt.SeasonInPlay || dt.PressPending is not null || dt.Week >= dt.Calendar.Count) return;
        var w = dt.Calendar[dt.Week];
        switch (w.Type)
        {
            case "match": PlayNextMatch(dt); break;   // el mensaje lo pone PlayNextMatch (resultado + lesiones)
            case "train": ApplyTraining(dt); break;
            case "press": dt.PressPending = GenPress(dt); dt.WeekMsg = "🎙️ Rueda de prensa"; break;
            case "rest":
                foreach (var p in dt.Squad) { p.Fatigue = Math.Max(0, p.Fatigue - 25); if (p.InjuryWeeks > 0) p.InjuryWeeks--; }
                dt.WeekMsg = "🛌 Semana de recuperación"; break;
        }
        w.Done = true;
        dt.Week++;
        if (dt.Week >= dt.Calendar.Count) EndSeason(dt);
    }

    /// <summary>Avanza (aplicando entrenamientos) hasta la próxima semana de partido, para que armes el equipo.</summary>
    public static void AdvanceToNextMatch(DtManager dt)
    {
        if (dt.CareerFinished) return;
        int guard = 0;
        while (dt.SeasonInPlay && dt.PressPending is null && dt.Week < dt.Calendar.Count
               && dt.Calendar[dt.Week].Type != "match" && guard++ < 60)
            AdvanceWeek(dt);
    }

    /// <summary>Simula el resto de la temporada (todos los meses). La prensa se responde sola en modo neutral.</summary>
    public static void SimRestOfSeason(DtManager dt)
    {
        if (dt.CareerFinished) return;
        int guard = 0;
        while (dt.SeasonInPlay && guard++ < 120)
        {
            if (dt.PressPending is not null) { AnswerPress(dt, dt.PressPending.Options.Last()); continue; }
            AdvanceWeek(dt);
        }
    }

    public static List<DtTableRow> Standings(DtManager dt) =>
        dt.Table.OrderByDescending(t => t.Pts).ThenByDescending(t => t.Diff).ThenByDescending(t => t.GF).ThenBy(t => t.Name).ToList();

    // ---------------------------------------------------------------- fin de temporada
    private static void EndSeason(DtManager dt)
    {
        dt.SeasonInPlay = false;
        var club = dt.Club!;
        var std = Standings(dt);
        int pos = std.FindIndex(t => t.IsMe) + 1;
        var me = dt.Table.First(t => t.IsMe);
        bool champ = pos == 1;
        bool relegated = pos >= Teams - 1;

        var titles = new List<string>();
        if (champ) titles.Add("🏆 Liga");
        if (Rng.NextDouble() < 0.12 + (Teams / 2.0 - pos) / 100.0) titles.Add("🏅 Copa");
        if (club.Level >= 3 && pos <= 2 && Rng.NextDouble() < (club.Level == 4 ? 0.35 : 0.18)) titles.Add("⭐ Internacional");

        int mediaCut = (int)Math.Ceiling(Teams * 0.6);
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

        int dConf = (objMet ? 16 : -22) + (champ ? 12 : 0) + titles.Count * 6 + (relegated ? -25 : 0);
        dt.Confidence = Math.Clamp(dt.Confidence + dConf, 0, 100);
        int dRep = titles.Count * 10 + (objMet ? 8 : -3) + (titles.Contains("⭐ Internacional") ? 10 : 0)
                 + (pos <= 3 ? 5 : 0) + (champ ? 4 : 0) - (relegated ? 6 : 0);
        dt.Rep = Math.Clamp(dt.Rep + dRep, 0, 100);

        dt.Matches += me.Played; dt.Wins += me.Won; dt.Draws += me.Drawn; dt.Losses += me.Lost;
        dt.Titles += titles.Count; dt.SeasonsManaged++;

        string note = titles.Count > 0 ? string.Join(" · ", titles)
                    : objMet ? "✔ Objetivos cumplidos"
                    : relegated ? "⬇ Descenso" : "✖ Objetivos incumplidos";

        // Finanzas.
        int upkeep = (dt.TrainCenter + dt.Youth + dt.Medical + dt.Scouting) * 3;
        int loanPay = dt.LoanSeasonsLeft > 0 ? (int)Math.Ceiling(dt.LoanRemainingM / (double)dt.LoanSeasonsLeft) : 0;
        if (loanPay > 0) { dt.LoanRemainingM -= loanPay; dt.LoanSeasonsLeft--; }
        int income = IncomeBase(club.Level) + Math.Max(0, Teams / 2 - pos) * 3 + titles.Count * 25 + (titles.Contains("⭐ Internacional") ? 60 : 0);
        int expense = WageBase(club.Level) + upkeep + loanPay;
        dt.CajaM += income - expense;
        dt.LastIncomeM = income; dt.LastExpenseM = expense;

        dt.Timeline.Insert(0, new DtSeason
        {
            Year = dt.Year, Club = club.Name, Pos = pos, Teams = Teams,
            Played = me.Played, Won = me.Won, Drawn = me.Drawn, Lost = me.Lost, GF = me.GF, GA = me.GA,
            Titles = titles, ObjectiveMet = objMet, RepAfter = dt.Rep, Note = note,
        });

        DevelopSquad(dt);
        EvolveWorld(dt);
        WorldAndNews(dt, pos, titles, champ);

        if (dt.SeasonsManaged >= MaxCareerSeasons)
        {
            dt.CareerFinished = true;
            dt.Pending = null;
            dt.WeekMsg = $"🏁 Carrera completada: {MaxCareerSeasons} temporadas dirigidas";
            return;
        }

        dt.Pending = PostSeason(dt, objMet, relegated);
    }

    private static void EnsureWorld(DtManager dt)
    {
        if (dt.WorldClub.Count == 0)
            foreach (var c in DtData.Clubs) dt.WorldClub[c.Name] = c.Strength;
    }

    /// <summary>El mundo evoluciona: los clubes suben o bajan de nivel y los jugadores
    /// reales crecen o declinan, temporada a temporada, como en la vida real.</summary>
    private static void EvolveWorld(DtManager dt)
    {
        EnsureWorld(dt);
        // Clubes: derivan hacia su base con ruido; algunos crecen, otros caen.
        foreach (var c in DtData.Clubs)
        {
            int cur = dt.WorldClub.TryGetValue(c.Name, out var v) ? v : c.Strength;
            int drift = Rng.Next(-2, 3) + (cur > c.Strength + 4 ? -1 : cur < c.Strength - 4 ? 1 : 0);
            if (Rng.NextDouble() < 0.10) drift += Rng.Next(-3, 4);   // saltos ocasionales
            dt.WorldClub[c.Name] = Math.Clamp(cur + drift, c.Strength - 8, c.Strength + 8);
        }
        // Jugadores reales: los jóvenes/actuales crecen o declinan; las leyendas apenas bajan.
        foreach (var p in PlayerDatabase.All)
        {
            if (p.Troll) continue;
            int d = dt.WorldPlayer.GetValueOrDefault(p.Id);
            if (p.IsLegend) { if (Rng.NextDouble() < 0.12) d = Math.Max(-6, d - 1); }
            else if (Rng.NextDouble() < 0.5)
            {
                int step = p.Rating >= 88 ? (Rng.NextDouble() < 0.6 ? -1 : 1)
                         : p.Rating <= 78 ? (Rng.NextDouble() < 0.6 ? 1 : -1)
                         : (Rng.Next(0, 2) == 0 ? -1 : 1);
                d = Math.Clamp(d + step, -6, 8);
            }
            if (d != 0) dt.WorldPlayer[p.Id] = d; else dt.WorldPlayer.Remove(p.Id);
        }
    }

    /// <summary>
    /// Envejece y desarrolla a toda la plantilla entre temporadas. Esto incluye
    /// tanto a los jugadores iniciales como a los fichajes: todos permanecen en
    /// el mismo ciclo de evolución mientras sigan en el club.
    ///
    /// La curva es deliberadamente gradual: los menores de 35 progresan según su
    /// edad, uso y potencial; desde los 35 empieza el declive y se acelera con la
    /// edad. El entrenamiento y las instalaciones mejoran el desarrollo joven,
    /// pero no eliminan el efecto natural de la edad.
    /// </summary>
    private static void DevelopSquad(DtManager dt)
    {
        int dev = dt.TrainCenter + dt.Youth;   // 2..10
        foreach (var p in dt.Squad)
        {
            p.Age++;
            int delta = p.Age switch
            {
                <= 21 => dev >= 6 ? 2 : 1,
                <= 26 => 1,
                <= 30 => 1,
                <= 34 => 1,
                35 or 36 => -1,
                37 or 38 => -1 - (Rng.NextDouble() < 0.35 ? 1 : 0),
                _ => -2,
            };

            if (delta > 0)
                p.Media = Math.Min(p.Potential, p.Media + delta);
            else if (delta < 0)
                p.Media = Math.Max(40, p.Media + delta);
            p.ContractYears = Math.Max(0, p.ContractYears - 1);
            p.ValueM = PlayerValue(p.Media, p.Age);
        }
        RecalcStrength(dt);
    }

    // ---------------------------------------------------------------- economía
    private static int LevelCap(int lvl) => lvl switch { 1 => 68, 2 => 79, 3 => 87, _ => 93 };
    private static int WageBase(int lvl) => lvl switch { 1 => 10, 2 => 32, 3 => 78, _ => 170 };
    private static int IncomeBase(int lvl) => lvl switch { 1 => 24, 2 => 66, 3 => 150, _ => 320 };

    public static void NewBudget(DtManager dt)
    {
        var c = dt.Club!;
        double conf = 0.6 + dt.Confidence / 100.0 * 0.7;
        dt.TransferBudgetM = Math.Max(2, (int)Math.Round(c.BudgetM * conf) + Math.Max(0, dt.CajaM) / 6);
        dt.SpentThisSeasonM = 0;
        dt.ScoutPool.Clear();
    }

    /// <summary>Refuerzo rápido: gasta del presupuesto y suma un jugador a la plantilla.</summary>
    public static void Reinforce(DtManager dt, int m)
    {
        var c = dt.Club!;
        m = Math.Clamp(m, 0, dt.TransferBudgetM);
        if (m <= 0) return;
        double costPer = c.Level switch { 1 => 2.5, 2 => 6.0, 3 => 14.0, _ => 30.0 } * (1.0 - (dt.Scouting - 1) * 0.08);
        int media = Math.Clamp(c.Strength - 3 + (int)Math.Floor(m / Math.Max(1.0, costPer)), 45, 96);
        var pos = (Position)Rng.Next(0, 4);
        var p = GenPlayer(media, pos); p.Media = media; p.ValueM = PlayerValue(media, p.Age);
        dt.Squad.Insert(0, p);
        dt.Signings.Insert(0, $"{p.Name} · {p.Pos} {p.Media}");
        dt.TransferBudgetM -= m; dt.SpentThisSeasonM += m;
        AutoLineup(dt); RecalcStrength(dt);
    }

    public static int FacilityLevel(DtManager dt, string which) => which switch { "train" => dt.TrainCenter, "youth" => dt.Youth, "medical" => dt.Medical, _ => dt.Scouting };
    public static int FacilityCost(int currentLevel) => currentLevel switch { 1 => 15, 2 => 30, 3 => 55, 4 => 90, _ => 0 };

    public static bool UpgradeFacility(DtManager dt, string which)
    {
        int lvl = FacilityLevel(dt, which);
        if (lvl >= 5) return false;
        int cost = FacilityCost(lvl);
        if (dt.CajaM < cost) return false;
        dt.CajaM -= cost;
        switch (which) { case "train": dt.TrainCenter++; break; case "youth": dt.Youth++; break; case "medical": dt.Medical++; break; default: dt.Scouting++; break; }
        return true;
    }

    public static void RequestLoan(DtManager dt, int m)
    {
        m = Math.Clamp(m, 0, 300);
        if (m <= 0) return;
        dt.CajaM += m;
        dt.LoanRemainingM += (int)Math.Round(m * 1.15);
        dt.LoanSeasonsLeft = 4;
    }

    // ---------------------------------------------------------------- mercado
    private static int ProspectValue(int media, int age) => PlayerValue(media, age);

    public static void Scout(DtManager dt, Position? pos, int minMedia)
    {
        var club = dt.Club!;
        int n = 3 + dt.Scouting;
        int techo = club.Strength + 2 + dt.Scouting * 2;
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
            list.Add(new DtProspect
            {
                Id = "p" + Guid.NewGuid().ToString("N")[..6],
                Name = $"{DtData.FirstNames[Rng.Next(DtData.FirstNames.Length)]} {DtData.LastNames[Rng.Next(DtData.LastNames.Length)]}",
                Nation = nat.Name, NationCode = nat.Code, Pos = pp,
                Age = age, Media = media, Potential = pot,
                ValueM = free ? 0 : ProspectValue(media, age),
                Free = free, Club = free ? "Libre" : DtData.Clubs[Rng.Next(DtData.Clubs.Count)].Name,
            });
        }
        dt.ScoutPool = list.OrderByDescending(p => p.Media).ToList();
    }

    public static string Sign(DtManager dt, string prospectId, int offerM)
    {
        var p = dt.ScoutPool.FirstOrDefault(x => x.Id == prospectId);
        if (p is null) return "";
        if (!p.Free)
        {
            if (dt.TransferBudgetM < offerM) return "No te alcanza el presupuesto de fichajes.";
            if (offerM < p.ValueM) return $"Rechazada: {p.Club} pide ${p.ValueM}M.";
            dt.TransferBudgetM -= offerM;
        }
        dt.Squad.Insert(0, new DtPlayer
        {
            Id = "sq" + Guid.NewGuid().ToString("N")[..7],
            Name = p.Name, Nation = p.Nation, NationCode = p.NationCode, Pos = p.Pos,
            Age = p.Age, Media = p.Media, Potential = p.Potential,
            Morale = 75, ContractYears = 4, SalaryM = PlayerSalary(p.Media), ValueM = Math.Max(1, p.ValueM),
        });
        dt.Signings.Insert(0, $"{p.Name} · {p.Pos} {p.Media}{(p.Free ? " (libre)" : "")}");
        if (dt.Signings.Count > 12) dt.Signings.RemoveAt(dt.Signings.Count - 1);
        dt.ScoutPool.RemoveAll(x => x.Id == p.Id);
        AutoLineup(dt); RecalcStrength(dt);
        return $"✔ Fichaste a {p.Name} (media {p.Media})";
    }

    // ---------------------------------------------------------------- mercado real (leyendas del juego)
    private static string Code3(string nation) => new string((nation ?? "").Where(char.IsLetter).Take(3).ToArray()).ToUpperInvariant();

    /// <summary>Media EFECTIVA de un jugador real: su base + cómo evolucionó en el mundo.</summary>
    public static int EffRating(DtManager dt, Player p) => Math.Clamp(p.Rating + dt.WorldPlayer.GetValueOrDefault(p.Id), 40, 99);

    /// <summary>
    /// Jugadores REALES del juego que podés fichar, como en el modo principal.
    /// era: "current" (actuales) · "legend" (leyendas) · "all" (todos).
    /// </summary>
    public static List<Player> RealMarket(DtManager dt, Position? pos, string search, string era)
    {
        var have = dt.Squad.Select(p => p.Name).ToHashSet();
        return PlayerDatabase.All
            .Where(p => !p.Troll && !have.Contains(p.Name)
                     && (era == "all" || (era == "legend" ? p.Era < 2015 : p.Era >= 2015))
                     && (pos is null || p.Pos == pos)
                     && (string.IsNullOrWhiteSpace(search) || p.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(p => EffRating(dt, p)).Take(24).ToList();
    }

    public static int RealPrice(DtManager dt, Player p)
    {
        int r = EffRating(dt, p);
        return Math.Max(1, (int)Math.Round(0.0034 * Math.Pow(Math.Max(1, r - 50), 2.95)));
    }

    /// <summary>Ficha a un jugador real del mercado (se paga del presupuesto de fichajes).</summary>
    public static string SignReal(DtManager dt, string playerId)
    {
        var p = PlayerDatabase.All.FirstOrDefault(x => x.Id == playerId);
        if (p is null) return "";
        int price = RealPrice(dt, p);
        if (dt.TransferBudgetM < price) return $"No te alcanza: cuesta ${price}M.";
        dt.TransferBudgetM -= price;
        int media = Math.Clamp(EffRating(dt, p), 45, 97);
        dt.Squad.Insert(0, new DtPlayer
        {
            Id = "sq" + Guid.NewGuid().ToString("N")[..7],
            Name = p.Name, Nation = p.Nation, NationCode = Code3(p.Nation), Pos = p.Pos,
            Age = 27, Media = media, Potential = Math.Min(99, media + 1),
            Morale = 80, ContractYears = 4, SalaryM = PlayerSalary(media), ValueM = price,
        });
        dt.Signings.Insert(0, $"{p.Name} · {p.Pos} {media}{(p.IsLegend ? " ⭐" : "")}");
        if (dt.Signings.Count > 12) dt.Signings.RemoveAt(dt.Signings.Count - 1);
        AutoLineup(dt); RecalcStrength(dt);
        return $"✔ Fichaste a {p.Name} (media {media})";
    }

    // ---------------------------------------------------------------- objetivos / decisiones
    private static List<DtObjective> ObjectivesFor(DtClub c) => c.Level switch
    {
        1 => new() { new() { Text = "No descender", Kind = "nodesc" }, new() { Text = "Terminar en la mitad de la tabla", Kind = "media" } },
        2 => new() { new() { Text = "Clasificar a una internacional (top 4)", Kind = "intl" }, new() { Text = "Ganar un título", Kind = "copa" } },
        3 => new() { new() { Text = "Pelear la liga (top 2)", Kind = "media" }, new() { Text = "Ganar un título", Kind = "copa" } },
        _ => new() { new() { Text = "Ganar la liga", Kind = "liga" }, new() { Text = "Ganar un torneo internacional", Kind = "intl" } },
    };

    private static DtDecision PostSeason(DtManager dt, bool objMet, bool relegated)
    {
        var club = dt.Club!;
        if (dt.Confidence < 22 && !objMet)
        {
            if (dt.Warned || relegated)
            {
                dt.Employed = false; dt.TimesFired++;
                dt.Rep = Math.Clamp(dt.Rep - 8, 0, 100);
                return NewJobDecision(dt, "🚪 Te echaron", $"La directiva de {club.Name} terminó tu ciclo. Buscá un nuevo proyecto.");
            }
            dt.Warned = true;
        }
        else dt.Warned = false;

        var d = new DtDecision
        {
            Title = objMet ? "Fin de temporada" : "Temporada difícil",
            Text = objMet ? "La directiva está conforme. Seguí el proyecto o escuchá ofertas." : "Los números no fueron los esperados. Decidí cómo seguir.",
        };
        d.Options.Add(new DtOption { Label = $"Seguir en {club.Name}", Sub = "renovar el proyecto", Kind = "stay", Club = club });

        int max = MaxLevel(dt.Rep);
        if (max > club.Level || (max == club.Level && dt.Rep >= 40))
        {
            var offers = Enumerable.Range(club.Level, max - club.Level + 1)
                .SelectMany(DtData.ByLevel).Where(c => c.Name != club.Name && c.Level >= club.Level)
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
        var pool = Enumerable.Range(1, max).SelectMany(DtData.ByLevel)
            .Where(c => c.Name != dt.Club?.Name).OrderBy(_ => Rng.Next()).Take(3).ToList();
        foreach (var c in pool)
            d.Options.Add(new DtOption { Label = $"Firmar por {c.Name}", Sub = $"{DtData.LevelName(c.Level)} · {c.League}", Kind = "move", Club = c });
        return d;
    }

    public static void Choose(DtManager dt, DtOption opt)
    {
        if (dt.CareerFinished) return;
        if (opt.Kind == "move" && opt.Club is not null) Hire(dt, opt.Club);   // nuevo club, nueva plantilla
        else StartSeason(dt);                                                  // seguir: nueva temporada con la misma plantilla
        dt.Year++;
    }

    // ---------------------------------------------------------------- mundo vivo
    private static string RandName() => $"{DtData.FirstNames[Rng.Next(DtData.FirstNames.Length)]} {DtData.LastNames[Rng.Next(DtData.LastNames.Length)]}";

    private static void WorldAndNews(DtManager dt, int pos, List<string> titles, bool champ)
    {
        var club = dt.Club!;
        dt.News.Clear();
        dt.News.Add(new DtNews { Icon = "📋", Text = $"{club.Name} terminó {pos}º" + (titles.Count > 0 ? $" y ganó {string.Join(", ", titles)}" : "") });
        var gigs = DtData.ByLevel(4).Where(c => c.Name != club.Name).OrderBy(_ => Rng.Next()).Take(3).ToList();
        foreach (var g in gigs) dt.News.Add(new DtNews { Icon = "🏆", Text = $"{g.Name} se coronó campeón de {g.League}" });
        var buyer = DtData.ByLevel(4).OrderBy(_ => Rng.Next()).First();
        dt.News.Add(new DtNews { Icon = "💸", Text = $"{buyer.Name} fichó a {RandName()} por ${Rng.Next(70, 190)}M" });
        dt.News.Add(new DtNews { Icon = "🌟", Text = $"{RandName()} ({Rng.Next(17, 20)} años) es la sensación de la temporada" });
        dt.News.Add(new DtNews { Icon = "🔁", Text = $"{DtData.Clubs[Rng.Next(DtData.Clubs.Count)].Name} cambió de entrenador" });

        // Balón de Oro: tu goleador si fuiste campeón, o una estrella cualquiera.
        var topScorer = dt.Squad.OrderByDescending(p => p.Goals).FirstOrDefault();
        string ballon = (champ && topScorer is not null && topScorer.Goals > 0) ? topScorer.Name : RandName();
        string bota = topScorer is not null && topScorer.Goals >= 12 ? topScorer.Name : RandName();

        bool youWin = (champ || titles.Contains("⭐ Internacional")) && Rng.NextDouble() < (club.Level >= 4 ? 0.55 : 0.30);
        string dtAward;
        if (youWin) { dt.Awards.Insert(0, $"🏅 DT del Año · T{dt.Year}"); dt.Rep = Math.Clamp(dt.Rep + 5, 0, 100); dtAward = $"{dt.Surname} — 🏅 DT del Año"; }
        else dtAward = $"{RandName()} — DT del Año";

        dt.News.Add(new DtNews { Icon = "🥇", Text = $"Balón de Oro: {ballon} · Bota de Oro: {bota}" });
        dt.News.Add(new DtNews { Icon = "🎖️", Text = dtAward });

        dt.WorldLog.Insert(0, $"T{dt.Year}: 🏆 {gigs.FirstOrDefault()?.Name ?? club.Name} · 🥇 {ballon}");
        if (dt.WorldLog.Count > 24) dt.WorldLog.RemoveAt(dt.WorldLog.Count - 1);
    }

    public static string Legacy(DtManager dt)
    {
        if (dt.Rep >= 80 && dt.Titles >= 12) return "🐐 Leyenda de los banquillos";
        if (dt.Rep >= 60 && dt.Titles >= 6) return "👑 DT de elite";
        if (dt.Rep >= 40) return "⭐ Entrenador reconocido";
        if (dt.Rep >= 20) return "🎯 Técnico en ascenso";
        return "🌱 Empezando el camino";
    }

    public static string PosName(Position p) => p switch { Position.GK => "POR", Position.DEF => "DFC", Position.MID => "MC", _ => "DC" };
}
