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
        string AttackerName(int team) =>
            PickName(team == 0 ? homeStarters : awayStarters, rng);
        string DefenderName(int team) =>
            PickDefender(team == 0 ? homeStarters : awayStarters, rng);
        string GkName(int team) =>
            GoalkeeperName(team == 0 ? homeStarters : awayStarters);

        void Add(SimEvent e) => events.Add(e);
        double C01(double v) => Math.Clamp(v, 0.06, 0.94);
        int Min(int sec) => Math.Min(120, sec / 60 + 1);

        Add(new SimEvent { Clock = 0, Type = SimEventType.KickOff, BallX = 0.5, BallY = 0.5, Text = "¡Comienza el partido!", Big = true, Dur = 0.8 });
        // (Actor/Target/ScoreH/ScoreA se llenan por evento para poder traducir el relato en el cliente)

        // Reparto de posesión sesgado por fuerza (acumulado para la estadística real)
        var possSecs = new double[2];
        double homeShare = Math.Pow(home.Overall, 1.7) / (Math.Pow(home.Overall, 1.7) + Math.Pow(away.Overall, 1.7));

        void Period(int startSec, int endSec, MatchPhase ph)
        {
            int t = startSec;
            while (t < endSec)
            {
                // el equipo más fuerte tiene la pelota más seguido
                int team = rng.NextDouble() < homeShare ? 0 : 1;
                var us = team == 0 ? home : away;
                var them = team == 0 ? away : home;
                string usName = team == 0 ? homeName : awayName;

                int dur = 12 + rng.Next(28);
                t += dur; possSecs[team] += dur;
                if (t > endSec) t = endSec;

                // Construcción de juego: pases/corridas hacia el arco rival
                int steps = 1 + rng.Next(3);
                double x = team == 0 ? 0.30 + rng.NextDouble() * 0.15 : 0.70 - rng.NextDouble() * 0.15;
                double y = 0.2 + rng.NextDouble() * 0.6;
                for (int s = 0; s < steps; s++)
                {
                    x = team == 0 ? Math.Min(0.93, x + 0.10 + rng.NextDouble() * 0.14)
                                  : Math.Max(0.07, x - 0.10 - rng.NextDouble() * 0.14);
                    y = C01(y + (rng.NextDouble() - 0.5) * 0.24);
                    Add(new SimEvent
                    {
                        Clock = t,
                        Type = s == 0 ? SimEventType.Pass : SimEventType.Carry,
                        Team = team, Player = 1 + rng.Next(10), BallX = x, BallY = y, Phase = ph, Dur = 0.45,
                    });
                }

                // Falta — con nombres: quién la hizo y a quién
                if (rng.NextDouble() < 0.14)
                {
                    int foulTeam = 1 - team;
                    string fouler = DefenderName(foulTeam);
                    string victim = AttackerName(team);
                    if (foulTeam == 0) stats.FoulsHome++; else stats.FoulsAway++;
                    Add(new SimEvent { Clock = t, Type = SimEventType.Foul, Team = foulTeam, BallX = x, BallY = y, Phase = ph, Dur = 0.7, Actor = fouler, Target = victim, Text = $"Falta de {fouler} sobre {victim}" });
                    double cr = rng.NextDouble();
                    if (cr < 0.03) { if (foulTeam == 0) stats.RedHome++; else stats.RedAway++; Add(new SimEvent { Clock = t, Type = SimEventType.Red, Team = foulTeam, BallX = x, BallY = y, Phase = ph, Dur = 1.2, Big = true, Actor = fouler, Text = $"🟥 Roja a {fouler}" }); }
                    else if (cr < 0.25) { if (foulTeam == 0) stats.YellowHome++; else stats.YellowAway++; Add(new SimEvent { Clock = t, Type = SimEventType.Yellow, Team = foulTeam, BallX = x, BallY = y, Phase = ph, Dur = 0.9, Actor = fouler, Text = $"🟨 Amarilla a {fouler}" }); }
                    // Lesión ocasional del jugador que recibió la falta
                    if (rng.NextDouble() < 0.06)
                        Add(new SimEvent { Clock = t, Type = SimEventType.Injury, Team = team, BallX = x, BallY = y, Phase = ph, Dur = 1.1, Actor = victim, Text = $"🚑 {victim} queda golpeado y necesita atención" });
                }

                bool finalThird = team == 0 ? x > 0.66 : x < 0.34;
                // "ventaja" 0..1 según la diferencia de fuerza (logística): pesa MUCHO,
                // así un equipo débil casi no genera peligro contra uno fuerte.
                double edge = 1.0 / (1.0 + Math.Exp(-(us.Attack - them.Defense) / 9.0));

                if (finalThird && rng.NextDouble() < 0.055)
                {
                    // Offside: la jugada se corta por posición adelantada
                    Add(new SimEvent { Clock = t, Type = SimEventType.Offside, Team = team, BallX = x, BallY = y, Phase = ph, Dur = 0.9, Actor = ScorerName(team), Text = $"🚩 Offside · posición adelantada de {ScorerName(team)}" });
                }
                else if (finalThird && rng.NextDouble() < 0.05 + 0.28 * edge)
                {
                    if (team == 0) stats.ShotsHome++; else stats.ShotsAway++;
                    double goalY = 0.5 + (rng.NextDouble() - 0.5) * 0.18;   // dentro del arco
                    double shotX = team == 0 ? 0.86 : 0.14;
                    string shooter = ScorerName(team);
                    Add(new SimEvent { Clock = t, Type = SimEventType.Shot, Team = team, Player = 1 + rng.Next(10), BallX = shotX, BallY = goalY, Phase = ph, Dur = 0.5, Actor = shooter, Target = usName, Text = $"Remate de {shooter} ({usName})" });

                    double xg = Math.Clamp(0.05 + 0.42 * edge, 0.03, 0.55);
                    double gx = team == 0 ? 0.985 : 0.015;                 // línea de gol
                    if (rng.NextDouble() < xg)
                    {
                        // GOL: la pelota entra al arco
                        score[team]++;
                        if (team == 0) stats.OnTargetHome++; else stats.OnTargetAway++;
                        goals.Add(new Goal(Min(t), shooter, team == 0));
                        Add(new SimEvent { Clock = t, Type = SimEventType.Goal, Team = team, Player = 1 + rng.Next(10), BallX = gx, BallY = goalY, Phase = ph, Dur = 2.2, Big = true, Actor = shooter, ScoreH = score[0], ScoreA = score[1], Text = $"⚽ ¡GOOOL de {shooter}!  ({score[0]}-{score[1]})" });
                    }
                    else
                    {
                        double r = rng.NextDouble();
                        if (r < 0.5)
                        {
                            // ATAJADA: la pelota queda en el arquero, NO entra
                            if (team == 0) stats.OnTargetHome++; else stats.OnTargetAway++;
                            double keepX = team == 0 ? 0.91 : 0.09;
                            Add(new SimEvent { Clock = t, Type = SimEventType.Save, Team = 1 - team, BallX = keepX, BallY = goalY, Phase = ph, Dur = 0.9, Actor = GkName(1 - team), Text = $"🧤 ¡Atajó {GkName(1 - team)}!" });
                            if (rng.NextDouble() < 0.5) { if (team == 0) stats.CornersHome++; else stats.CornersAway++; }
                        }
                        else if (r < 0.63)
                        {
                            // AL PALO: pega en el poste y sale
                            double postX = team == 0 ? 0.95 : 0.05;
                            double postY = goalY < 0.5 ? 0.40 : 0.60;
                            Add(new SimEvent { Clock = t, Type = SimEventType.Post, Team = team, BallX = postX, BallY = postY, Phase = ph, Dur = 0.9, Actor = shooter, Text = $"😱 ¡{shooter} al palo!" });
                        }
                        else
                        {
                            // AFUERA: desviado, la pelota se va lejos del arco
                            double wideX = team == 0 ? 0.97 : 0.03;
                            double wideY = rng.NextDouble() < 0.5 ? 0.22 : 0.78;
                            Add(new SimEvent { Clock = t, Type = SimEventType.Miss, Team = team, BallX = wideX, BallY = wideY, Phase = ph, Dur = 0.6, Actor = shooter, Text = $"Remate desviado de {shooter}" });
                        }
                    }
                }
            }
        }

        // 1º TIEMPO — pausa de hidratación al minuto 22 (regla Mundial 2026)
        Period(0, 22 * 60, MatchPhase.First);
        Add(new SimEvent { Clock = 22 * 60, Type = SimEventType.HydrationBreak, BallX = 0.5, BallY = 0.5, Phase = MatchPhase.First, Dur = 1.4, Actor = "22", Text = "💧 Pausa de hidratación (min 22)" });
        Period(22 * 60, 45 * 60, MatchPhase.First);
        int add1 = 1 + rng.Next(4);   // 1..4 min de añadido
        Period(45 * 60, (45 + add1) * 60, MatchPhase.First);
        Add(new SimEvent { Clock = (45 + add1) * 60, Type = SimEventType.HalfTime, BallX = 0.5, BallY = 0.5, Phase = MatchPhase.Half, Dur = 1.6, Big = true, ScoreH = score[0], ScoreA = score[1], Text = $"⏸ Entretiempo · {score[0]}-{score[1]}" });

        // 2º TIEMPO — pausa de hidratación al minuto 67 (22' del 2T)
        Add(new SimEvent { Clock = 45 * 60, Type = SimEventType.SecondHalf, BallX = 0.5, BallY = 0.5, Phase = MatchPhase.Second, Dur = 0.8, Text = "Arranca el segundo tiempo" });
        Period(45 * 60, 67 * 60, MatchPhase.Second);
        Add(new SimEvent { Clock = 67 * 60, Type = SimEventType.HydrationBreak, BallX = 0.5, BallY = 0.5, Phase = MatchPhase.Second, Dur = 1.4, Actor = "67", Text = "💧 Pausa de hidratación (min 67)" });
        Period(67 * 60, 90 * 60, MatchPhase.Second);
        int add2 = 2 + rng.Next(5);   // 2..6 min de añadido
        Period(90 * 60, (90 + add2) * 60, MatchPhase.Second);
        Add(new SimEvent { Clock = (90 + add2) * 60, Type = SimEventType.FullTime, BallX = 0.5, BallY = 0.5, Phase = MatchPhase.Second, Dur = 1.4, Big = true, ScoreH = score[0], ScoreA = score[1], Text = $"⏱ Final del partido · {score[0]}-{score[1]}" });

        bool extra = false, shootout = false;
        int homePens = 0, awayPens = 0;

        if (knockout && score[0] == score[1])
        {
            extra = true;
            Add(new SimEvent { Clock = 90 * 60, Type = SimEventType.ExtraTimeStart, BallX = 0.5, BallY = 0.5, Phase = MatchPhase.ExtraFirst, Dur = 1.4, Big = true, Text = "⏳ ¡Tiempo suplementario!" });
            Period(90 * 60, 105 * 60, MatchPhase.ExtraFirst);
            Period(105 * 60, 120 * 60, MatchPhase.ExtraSecond);
            Add(new SimEvent { Clock = 120 * 60, Type = SimEventType.FullTime, BallX = 0.5, BallY = 0.5, Phase = MatchPhase.ExtraSecond, Dur = 1.2, Big = true, ScoreH = score[0], ScoreA = score[1], Text = $"⏱ 120' · {score[0]}-{score[1]}" });
        }

        if (knockout && score[0] == score[1])
        {
            shootout = true;
            Add(new SimEvent { Clock = 120 * 60, Type = SimEventType.ShootoutStart, BallX = 0.5, BallY = 0.5, Phase = MatchPhase.Shootout, Dur = 1.4, Big = true, Text = "🎯 ¡Definición por penales!" });
            double convH = 0.72 + (home.Overall - away.Overall) / 500.0;
            double convA = 0.72 + (away.Overall - home.Overall) / 500.0;
            int kh = 0, ka = 0;
            var hTakers = TakerOrder(homeStarters, homeName);
            var aTakers = TakerOrder(awayStarters, awayName);

            SimEvent Pen(int team, bool scored, int kickIdx)
            {
                var takers = team == 0 ? hTakers : aTakers;
                string taker = takers[kickIdx % takers.Count];
                string gk = GoalkeeperName(team == 0 ? awayStarters : homeStarters);
                return new()
                {
                    Clock = 120 * 60,
                    Type = scored ? SimEventType.PenaltyGoal : SimEventType.PenaltyMiss,
                    Team = team, BallX = team == 0 ? 0.99 : 0.01, BallY = 0.5,
                    Phase = MatchPhase.Shootout, Dur = 1.1, Big = scored,
                    Actor = scored ? taker : gk, Target = taker,
                    ScoreH = homePens, ScoreA = awayPens,
                    Text = scored
                        ? $"✅ {taker} anota  ({homePens}-{awayPens})"
                        : $"❌ {gk} le ataja a {taker}  ({homePens}-{awayPens})",
                };
            }

            bool done = false;
            for (int i = 0; i < 5 && !done; i++)
            {
                bool s = rng.NextDouble() < convH; if (s) homePens++; int ih = kh; kh++;
                Add(Pen(0, s, ih));
                if (homePens > awayPens + (5 - ka) || awayPens > homePens + (5 - kh)) { done = true; break; }

                bool s2 = rng.NextDouble() < convA; if (s2) awayPens++; int ia = ka; ka++;
                Add(Pen(1, s2, ia));
                if (homePens > awayPens + (5 - ka) || awayPens > homePens + (5 - kh)) { done = true; break; }
            }
            int guard = 0;
            while (homePens == awayPens && guard++ < 20)
            {
                bool s = rng.NextDouble() < convH; if (s) homePens++; Add(Pen(0, s, kh)); kh++;
                bool s2 = rng.NextDouble() < convA; if (s2) awayPens++; Add(Pen(1, s2, ka)); ka++;
            }
            if (homePens == awayPens) homePens++;
        }

        double totPoss = possSecs[0] + possSecs[1];
        if (totPoss > 0) stats.PossHome = (int)Math.Round(100 * possSecs[0] / totPoss);

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

    // Un defensor/mediocampista (el que suele cometer la falta).
    private static string PickDefender(IReadOnlyList<Player> starters, Random rng)
    {
        if (starters.Count == 0) return "N°4";
        var pool = starters.Where(p => p.Pos is Position.DEF or Position.MID).ToList();
        if (pool.Count == 0) pool = starters.ToList();
        return pool[rng.Next(pool.Count)].Name;
    }

    private static string GoalkeeperName(IReadOnlyList<Player> starters)
    {
        var gk = starters.FirstOrDefault(p => p.Pos == Position.GK);
        return gk?.Name ?? "el arquero";
    }

    // Orden de pateadores de penal: delanteros primero, luego mediocampistas.
    private static List<string> TakerOrder(IReadOnlyList<Player> starters, string fallback)
    {
        if (starters is null || starters.Count == 0) return new() { fallback };
        return starters
            .OrderByDescending(p => p.Pos == Position.FWD ? 3 : p.Pos == Position.MID ? 2 : p.Pos == Position.DEF ? 1 : 0)
            .ThenByDescending(p => p.Rating)
            .Select(p => p.Name).ToList();
    }
}
