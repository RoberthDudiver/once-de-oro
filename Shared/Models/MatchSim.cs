namespace OnceDeOro.Models;

/// <summary>Tipos de evento dentro de un partido simulado.</summary>
public enum SimEventType
{
    KickOff, Pass, Carry, Tackle, Foul, Yellow, Red, Shot, Save, Post, Miss, Goal,
    Corner, Offside, HydrationBreak, HalfTime, SecondHalf, FullTime,
    ExtraTimeStart, ShootoutStart, PenaltyGoal, PenaltyMiss, End
}

/// <summary>Fase del partido (para el reloj y los rótulos).</summary>
public enum MatchPhase { First, Half, Second, ExtraFirst, ExtraSecond, Shootout, Ended }

/// <summary>
/// Un instante del partido: dónde está la pelota, quién la tiene y qué pasa.
/// Coordenadas normalizadas 0..1 (x: 0 = arco local, 1 = arco visitante; y: 0..1 arriba→abajo).
/// </summary>
public sealed class SimEvent
{
    public int Clock { get; set; }            // segundo de juego (para mostrar el minuto)
    public double Dur { get; set; } = 0.6;    // duración de animación en segundos
    public SimEventType Type { get; set; }
    public int Team { get; set; } = -1;       // 0 local, 1 visitante, -1 neutro
    public int Player { get; set; } = -1;     // índice 0..10 del jugador en cuestión
    public double BallX { get; set; } = 0.5;
    public double BallY { get; set; } = 0.5;
    public string Text { get; set; } = "";    // línea del relato
    public MatchPhase Phase { get; set; } = MatchPhase.First;
    public bool Big { get; set; }             // resaltar (gol, tarjeta, penal)
}

public sealed class MatchStats
{
    public int PossHome { get; set; } = 50;
    public int ShotsHome { get; set; }
    public int ShotsAway { get; set; }
    public int OnTargetHome { get; set; }
    public int OnTargetAway { get; set; }
    public int FoulsHome { get; set; }
    public int FoulsAway { get; set; }
    public int YellowHome { get; set; }
    public int YellowAway { get; set; }
    public int RedHome { get; set; }
    public int RedAway { get; set; }
    public int CornersHome { get; set; }
    public int CornersAway { get; set; }
    public int PossAway => 100 - PossHome;
}

/// <summary>Todo lo necesario para reproducir el partido animado.</summary>
public sealed class MatchTimeline
{
    public required MatchResult Result { get; init; }
    public required MatchStats Stats { get; init; }
    public List<SimEvent> Events { get; init; } = new();
    public bool Knockout { get; init; }
    public bool WentExtraTime { get; set; }
    public bool WentShootout { get; set; }
}

/// <summary>Predicción previa al partido con modelos reconocidos (Poisson + Elo).</summary>
public sealed class Prediction
{
    public double HomeWin { get; set; }
    public double Draw { get; set; }
    public double AwayWin { get; set; }
    public double XgHome { get; set; }
    public double XgAway { get; set; }
    public int EloHome { get; set; }
    public int EloAway { get; set; }
    public double EloHomeWin { get; set; }         // prob. de victoria local por Elo
    public List<(int H, int A, double P)> TopScores { get; set; } = new();
}
