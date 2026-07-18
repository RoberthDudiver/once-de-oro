using OnceDeOro.Models;

namespace OnceDeOro.Services;

/// <summary>Poder desglosado de un equipo, de 0 a 99.</summary>
public readonly record struct TeamPower(int Overall, int Attack, int Defense)
{
    public static TeamPower Flat(int s) => new(s, s, s);
}

/// <summary>Motor de simulación: convierte poder + estilo en un partido gol a gol.</summary>
public sealed class MatchEngine
{
    private readonly Random _rng = new();

    // ---- Cálculo de poder a partir del XI ----
    public static TeamPower PowerOf(IReadOnlyList<Player> starters, TeamStyle style)
    {
        if (starters.Count == 0) return TeamPower.Flat(50);

        double Avg(Position p, double fallback)
        {
            var g = starters.Where(x => x.Pos == p).ToList();
            return g.Count == 0 ? fallback : g.Average(x => x.Rating);
        }

        double fwd = Avg(Position.FWD, 58);
        double mid = Avg(Position.MID, 58);
        double def = Avg(Position.DEF, 58);
        double gk = Avg(Position.GK, 58);

        double attack = 0.55 * fwd + 0.35 * mid + 0.10 * def;
        double defense = 0.45 * def + 0.35 * gk + 0.20 * mid;
        double overall = starters.Average(x => x.Rating);

        switch (style)
        {
            case TeamStyle.Ofensivo: attack += 4; defense -= 3; break;
            case TeamStyle.Defensivo: attack -= 3; defense += 5; break;
        }

        int Clamp(double v) => (int)Math.Round(Math.Clamp(v, 30, 99));
        return new TeamPower(Clamp(overall), Clamp(attack), Clamp(defense));
    }

    // ---- Simulación de un partido ----
    /// <param name="allowPenalties">true en eliminatorias: si hay empate, va a penales.</param>
    public MatchResult Simulate(
        string homeName, string homeFlag, TeamPower home, IReadOnlyList<Player> homeStarters,
        string awayName, string awayFlag, TeamPower away, bool allowPenalties)
    {
        var result = new MatchResult
        {
            HomeName = homeName,
            AwayName = awayName,
            HomeFlag = homeFlag,
            AwayFlag = awayFlag,
        };

        double xgHome = ExpectedGoals(home.Attack, away.Defense);
        double xgAway = ExpectedGoals(away.Attack, home.Defense);

        double pHome = xgHome / 90.0;
        double pAway = xgAway / 90.0;

        for (int minute = 1; minute <= 90; minute++)
        {
            if (_rng.NextDouble() < pHome)
            {
                result.HomeGoals++;
                result.Goals.Add(new Goal(minute, PickScorer(homeStarters, homeName), true));
            }
            if (_rng.NextDouble() < pAway)
            {
                result.AwayGoals++;
                result.Goals.Add(new Goal(minute, RivalScorer(awayName), false));
            }
        }

        result.Goals.Sort((a, b) => a.Minute.CompareTo(b.Minute));

        if (allowPenalties && result.HomeGoals == result.AwayGoals)
            Shootout(result, home.Overall, away.Overall);

        return result;
    }

    /// <summary>Simulación jugador-vs-jugador: ambos equipos aportan sus goleadores reales.</summary>
    public MatchResult SimulatePvP(
        string homeName, string homeFlag, TeamPower home, IReadOnlyList<Player> homeStarters,
        string awayName, string awayFlag, TeamPower away, IReadOnlyList<Player> awayStarters,
        bool allowPenalties)
    {
        var result = new MatchResult
        {
            HomeName = homeName,
            AwayName = awayName,
            HomeFlag = homeFlag,
            AwayFlag = awayFlag,
        };

        double pHome = ExpectedGoals(home.Attack, away.Defense) / 90.0;
        double pAway = ExpectedGoals(away.Attack, home.Defense) / 90.0;

        for (int minute = 1; minute <= 90; minute++)
        {
            if (_rng.NextDouble() < pHome)
            {
                result.HomeGoals++;
                result.Goals.Add(new Goal(minute, PickScorer(homeStarters, homeName), true));
            }
            if (_rng.NextDouble() < pAway)
            {
                result.AwayGoals++;
                result.Goals.Add(new Goal(minute, PickScorer(awayStarters, awayName), false));
            }
        }

        result.Goals.Sort((a, b) => a.Minute.CompareTo(b.Minute));

        if (allowPenalties && result.HomeGoals == result.AwayGoals)
            Shootout(result, home.Overall, away.Overall);

        return result;
    }

    private double ExpectedGoals(int attack, int defense)
    {
        double diff = attack - defense;
        double xg = 1.35 * Math.Exp(diff / 16.0);
        return Math.Clamp(xg, 0.18, 4.6);
    }

    private void Shootout(MatchResult r, int homeStr, int awayStr)
    {
        r.WentToPenalties = true;
        double convHome = 0.68 + (homeStr - awayStr) / 400.0;
        double convAway = 0.68 + (awayStr - homeStr) / 400.0;

        // 5 tiros; si sigue empate, muerte súbita.
        for (int i = 0; i < 5; i++)
        {
            if (_rng.NextDouble() < convHome) r.HomePens++;
            if (_rng.NextDouble() < convAway) r.AwayPens++;
        }
        int guard = 0;
        while (r.HomePens == r.AwayPens && guard++ < 20)
        {
            bool h = _rng.NextDouble() < convHome;
            bool a = _rng.NextDouble() < convAway;
            if (h) r.HomePens++;
            if (a) r.AwayPens++;
        }
        if (r.HomePens == r.AwayPens) r.HomePens++; // desempate forzado
    }

    // El goleador propio se elige entre los titulares, ponderado por posición y fuerza.
    private string PickScorer(IReadOnlyList<Player> starters, string fallback)
    {
        if (starters.Count == 0) return fallback;
        var weighted = starters.Select(p => (p, w: ScoreWeight(p))).ToList();
        double total = weighted.Sum(x => x.w);
        double roll = _rng.NextDouble() * total;
        foreach (var (p, w) in weighted)
        {
            roll -= w;
            if (roll <= 0) return p.Name;
        }
        return weighted[^1].p.Name;
    }

    private static double ScoreWeight(Player p)
    {
        double posW = p.Pos switch
        {
            Position.FWD => 1.0,
            Position.MID => 0.5,
            Position.DEF => 0.12,
            Position.GK => 0.01,
            _ => 0.1
        };
        return posW * (p.Rating / 70.0);
    }

    // Goleadores rivales: nombres genéricos estables por equipo.
    private static readonly string[] RivalSurnames =
        { "Costa", "Vega", "Romero", "Bianchi", "Novak", "Petit", "König", "Silva",
          "Moretti", "Ferreira", "Adé", "Ivanov", "Krause", "Marín", "Blanc", "Sørensen" };

    private string RivalScorer(string teamName)
    {
        int seed = Math.Abs(teamName.GetHashCode());
        var a = RivalSurnames[(seed + _rng.Next(4)) % RivalSurnames.Length];
        return a;
    }
}
