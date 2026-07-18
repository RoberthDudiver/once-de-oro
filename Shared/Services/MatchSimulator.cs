using OnceDeOro.Models;

namespace OnceDeOro.Services;

/// <summary>
/// Motor de simulación por eventos: recrea el partido jugada a jugada
/// (pases, corridas, faltas, tarjetas, tiros, hidratación, prórroga, penales),
/// con estadísticas. La predicción usa modelos reconocidos: Poisson + Elo.
/// </summary>
public static class MatchSimulator
{
    // ---------------------------------------------------------------- PREDICCIÓN
    private const double HomeEdge = 0.12;

    public static double Lambda(int attack, int defense)
        => Math.Clamp(1.35 * Math.Exp((attack - defense) / 16.0), 0.18, 4.6);

    private static double Poisson(double lambda, int k)
    {
        double p = Math.Exp(-lambda);
        for (int i = 1; i <= k; i++) p *= lambda / i;
        return p;
    }

    /// <summary>Predicción previa: Poisson para el marcador + Elo para el favoritismo.</summary>
    public static Prediction Predict(TeamPower home, TeamPower away)
    {
        double lh = Lambda(home.Attack, away.Defense) + HomeEdge;
        double la = Lambda(away.Attack, home.Defense);

        double pH = 0, pD = 0, pA = 0;
        var scores = new List<(int H, int A, double P)>();
        for (int h = 0; h <= 8; h++)
            for (int a = 0; a <= 8; a++)
            {
                double p = Poisson(lh, h) * Poisson(la, a);
                scores.Add((h, a, p));
                if (h > a) pH += p; else if (h == a) pD += p; else pA += p;
            }
        double tot = pH + pD + pA;
        if (tot <= 0) tot = 1;

        int eloH = 1000 + home.Overall * 12;
        int eloA = 1000 + away.Overall * 12;
        double eloHomeWin = 1.0 / (1.0 + Math.Pow(10, (eloA - eloH) / 400.0));

        return new Prediction
        {
            HomeWin = pH / tot,
            Draw = pD / tot,
            AwayWin = pA / tot,
            XgHome = lh,
            XgAway = la,
            EloHome = eloH,
            EloAway = eloA,
            EloHomeWin = eloHomeWin,
            TopScores = scores.OrderByDescending(s => s.P).Take(4)
                              .Select(s => (s.H, s.A, s.P / tot)).ToList(),
        };
    }

    // ---------------------------------------------------------------- SIMULACIÓN
    public static MatchTimeline Simulate(
        string homeName, string homeFlag, TeamPower home, IReadOnlyList<Player> homeStarters,
        string awayName, string awayFlag, TeamPower away, IReadOnlyList<Player> awayStarters,
        bool knockout, int seed)
    {
        var rng = new Random(seed);
        var events = new List<SimEvent>();
        var stats = new MatchStats();
        var goals = new List<Goal>();
        int[] score = { 0, 0 };

        double hp = home.Overall, ap = away.Overall;
        stats.PossHome = (int)Math.Round(100.0 * (hp + 3) / (hp + ap + 6));

        string ScorerName(int team) =>
            PickName(team == 0 ? homeStarters : awayStarters, rng);

        void Add(SimEvent e) => events.Add(e);
        double C01(double v) => Math.Clamp(v, 0.06, 0.94);
        int Min(int sec) => Math.Min(120, sec / 60 + 1);

        Add(new SimEvent { Clock = 0, Type = SimEventType.KickOff, BallX = 0.5, BallY = 0.5, Text = "¡Comienza el partido!", Big = true, Dur = 0.8 });

        void Period(int startSec, int endSec, MatchPhase ph)
        {
            int t = startSec;
            int poss = rng.Next(2);
            while (t < endSec)
            {
                int team = poss;
                var us = team == 0 ? home : away;
                var them = team == 0 ? away : home;
                string usName = team == 0 ? homeName : awayName;

                t += 12 + rng.Next(28);
                if (t > endSec) t = endSec;

                // Construcción de juego: pases/corridas hacia el arco rival
                int steps = 1 + rng.Next(3);
                double x = team == 0 ? 0.30 + rng.NextDouble() * 0.15 : 0.70 - rng.NextDouble() * 0.15;
                double y = 0.2 + rng.NextDouble() * 0.6;
                for (int s = 0; s < steps; s++)
                {
                    x = team == 0 ? Math.Min(0.92, x + 0.07 + rng.NextDouble() * 0.12)
                                  : Math.Max(0.08, x - 0.07 - rng.NextDouble() * 0.12);
                    y = C01(y + (rng.NextDouble() - 0.5) * 0.24);
                    Add(new SimEvent
                    {
                        Clock = t,
                        Type = s == 0 ? SimEventType.Pass : SimEventType.Carry,
                        Team = team, Player = rng.Next(11), BallX = x, BallY = y, Phase = ph, Dur = 0.45,
                    });
                }

                // Falta
                if (rng.NextDouble() < 0.14)
                {
                    int foulTeam = 1 - team;
                    if (foulTeam == 0) stats.FoulsHome++; else stats.FoulsAway++;
                    Add(new SimEvent { Clock = t, Type = SimEventType.Foul, Team = foulTeam, BallX = x, BallY = y, Phase = ph, Dur = 0.7, Text = "Falta" });
                    double cr = rng.NextDouble();
                    if (cr < 0.03) { if (foulTeam == 0) stats.RedHome++; else stats.RedAway++; Add(new SimEvent { Clock = t, Type = SimEventType.Red, Team = foulTeam, BallX = x, BallY = y, Phase = ph, Dur = 1.1, Text = "🟥 ¡Roja!", Big = true }); }
                    else if (cr < 0.25) { if (foulTeam == 0) stats.YellowHome++; else stats.YellowAway++; Add(new SimEvent { Clock = t, Type = SimEventType.Yellow, Team = foulTeam, BallX = x, BallY = y, Phase = ph, Dur = 0.9, Text = "🟨 Amarilla" }); }
                }

                bool finalThird = team == 0 ? x > 0.66 : x < 0.34;
                double atk = us.Attack / (double)(us.Attack + them.Defense);
                if (finalThird && rng.NextDouble() < 0.13 * atk + 0.05)
                {
                    if (team == 0) stats.ShotsHome++; else stats.ShotsAway++;
                    double goalY = 0.5 + (rng.NextDouble() - 0.5) * 0.20;
                    double shotX = team == 0 ? 0.90 : 0.10;
                    Add(new SimEvent { Clock = t, Type = SimEventType.Shot, Team = team, Player = rng.Next(11), BallX = shotX, BallY = goalY, Phase = ph, Dur = 0.5, Text = $"Remate de {usName}" });

                    double xg = Math.Clamp(0.06 + 0.17 * atk, 0.05, 0.42);
                    double gx = team == 0 ? 0.99 : 0.01;
                    if (rng.NextDouble() < xg)
                    {
                        score[team]++;
                        if (team == 0) stats.OnTargetHome++; else stats.OnTargetAway++;
                        string sc = ScorerName(team);
                        goals.Add(new Goal(Min(t), sc, team == 0));
                        Add(new SimEvent { Clock = t, Type = SimEventType.Goal, Team = team, Player = rng.Next(11), BallX = gx, BallY = goalY, Phase = ph, Dur = 1.4, Big = true, Text = $"⚽ ¡GOL! {sc}  ({score[0]}-{score[1]})" });
                        poss = 1 - team;
                    }
                    else
                    {
                        double r = rng.NextDouble();
                        if (r < 0.5) { if (team == 0) stats.OnTargetHome++; else stats.OnTargetAway++; Add(new SimEvent { Clock = t, Type = SimEventType.Save, Team = 1 - team, BallX = gx, BallY = goalY, Phase = ph, Dur = 0.8, Text = "🧤 ¡Gran atajada!" }); if (rng.NextDouble() < 0.5) { if (team == 0) stats.CornersHome++; else stats.CornersAway++; } }
                        else if (r < 0.63) Add(new SimEvent { Clock = t, Type = SimEventType.Post, Team = team, BallX = gx, BallY = goalY, Phase = ph, Dur = 0.9, Text = "😱 ¡Al palo!" });
                        else Add(new SimEvent { Clock = t, Type = SimEventType.Miss, Team = team, BallX = gx, BallY = goalY - 0.12, Phase = ph, Dur = 0.6, Text = "Desviado" });
                        poss = 1 - team;
                    }
                }
                else poss = 1 - team;
            }
        }

        // Primer tiempo con pausa de hidratación al minuto 30
        Period(0, 30 * 60, MatchPhase.First);
        Add(new SimEvent { Clock = 30 * 60, Type = SimEventType.HydrationBreak, BallX = 0.5, BallY = 0.5, Phase = MatchPhase.First, Dur = 1.4, Text = "💧 Pausa de hidratación" });
        Period(30 * 60, 45 * 60, MatchPhase.First);

        Add(new SimEvent { Clock = 45 * 60, Type = SimEventType.HalfTime, BallX = 0.5, BallY = 0.5, Phase = MatchPhase.Half, Dur = 1.6, Big = true, Text = $"⏸ Entretiempo · {score[0]}-{score[1]}" });
        Add(new SimEvent { Clock = 45 * 60, Type = SimEventType.SecondHalf, BallX = 0.5, BallY = 0.5, Phase = MatchPhase.Second, Dur = 0.8, Text = "Arranca el segundo tiempo" });

        Period(45 * 60, 75 * 60, MatchPhase.Second);
        Add(new SimEvent { Clock = 75 * 60, Type = SimEventType.HydrationBreak, BallX = 0.5, BallY = 0.5, Phase = MatchPhase.Second, Dur = 1.4, Text = "💧 Pausa de hidratación" });
        Period(75 * 60, 90 * 60, MatchPhase.Second);

        Add(new SimEvent { Clock = 90 * 60, Type = SimEventType.FullTime, BallX = 0.5, BallY = 0.5, Phase = MatchPhase.Second, Dur = 1.4, Big = true, Text = $"⏱ 90' · {score[0]}-{score[1]}" });

        bool extra = false, shootout = false;
        int homePens = 0, awayPens = 0;

        if (knockout && score[0] == score[1])
        {
            extra = true;
            Add(new SimEvent { Clock = 90 * 60, Type = SimEventType.ExtraTimeStart, BallX = 0.5, BallY = 0.5, Phase = MatchPhase.ExtraFirst, Dur = 1.4, Big = true, Text = "⏳ ¡Tiempo suplementario!" });
            Period(90 * 60, 105 * 60, MatchPhase.ExtraFirst);
            Period(105 * 60, 120 * 60, MatchPhase.ExtraSecond);
            Add(new SimEvent { Clock = 120 * 60, Type = SimEventType.FullTime, BallX = 0.5, BallY = 0.5, Phase = MatchPhase.ExtraSecond, Dur = 1.2, Big = true, Text = $"⏱ 120' · {score[0]}-{score[1]}" });
        }

        if (knockout && score[0] == score[1])
        {
            shootout = true;
            Add(new SimEvent { Clock = 120 * 60, Type = SimEventType.ShootoutStart, BallX = 0.5, BallY = 0.5, Phase = MatchPhase.Shootout, Dur = 1.4, Big = true, Text = "🎯 ¡Definición por penales!" });
            double convH = 0.72 + (home.Overall - away.Overall) / 500.0;
            double convA = 0.72 + (away.Overall - home.Overall) / 500.0;
            int kh = 0, ka = 0;

            SimEvent Pen(int team, bool scored, string name) => new()
            {
                Clock = 120 * 60,
                Type = scored ? SimEventType.PenaltyGoal : SimEventType.PenaltyMiss,
                Team = team, BallX = team == 0 ? 0.99 : 0.01, BallY = 0.5,
                Phase = MatchPhase.Shootout, Dur = 1.0, Big = scored,
                Text = scored ? $"✅ {name} anota  ({homePens}-{awayPens})" : $"❌ {name} la falla  ({homePens}-{awayPens})",
            };

            bool done = false;
            for (int i = 0; i < 5 && !done; i++)
            {
                bool s = rng.NextDouble() < convH; if (s) homePens++; kh++;
                Add(Pen(0, s, homeName));
                if (homePens > awayPens + (5 - ka) || awayPens > homePens + (5 - kh)) { done = true; break; }

                bool s2 = rng.NextDouble() < convA; if (s2) awayPens++; ka++;
                Add(Pen(1, s2, awayName));
                if (homePens > awayPens + (5 - ka) || awayPens > homePens + (5 - kh)) { done = true; break; }
            }
            int guard = 0;
            while (homePens == awayPens && guard++ < 20)
            {
                bool s = rng.NextDouble() < convH; if (s) homePens++; kh++; Add(Pen(0, s, homeName));
                bool s2 = rng.NextDouble() < convA; if (s2) awayPens++; ka++; Add(Pen(1, s2, awayName));
            }
            if (homePens == awayPens) homePens++;
        }

        var result = new MatchResult
        {
            HomeName = homeName, AwayName = awayName, HomeFlag = homeFlag, AwayFlag = awayFlag,
            HomeGoals = score[0], AwayGoals = score[1],
            WentToPenalties = shootout, HomePens = homePens, AwayPens = awayPens,
            Goals = goals,
        };

        Add(new SimEvent { Clock = 120 * 60, Type = SimEventType.End, BallX = 0.5, BallY = 0.5, Phase = MatchPhase.Ended, Dur = 0.6, Text = "Final" });

        return new MatchTimeline
        {
            Result = result, Stats = stats, Events = events,
            Knockout = knockout, WentExtraTime = extra, WentShootout = shootout,
        };
    }

    private static string PickName(IReadOnlyList<Player> starters, Random rng)
    {
        if (starters.Count == 0) return "N°9";
        var weighted = starters.Select(p => (p, w: p.Pos switch
        {
            Position.FWD => 1.0,
            Position.MID => 0.5,
            Position.DEF => 0.12,
            _ => 0.02
        } * (p.Rating / 70.0))).ToList();
        double total = weighted.Sum(x => x.w);
        double roll = rng.NextDouble() * total;
        foreach (var (p, w) in weighted) { roll -= w; if (roll <= 0) return p.Name; }
        return weighted[^1].p.Name;
    }
}
