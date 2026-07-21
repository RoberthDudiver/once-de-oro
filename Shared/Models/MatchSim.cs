namespace OnceDeOro.Models;

/// <summary>Tipos de evento dentro de un partido simulado.</summary>
public enum SimEventType
{
    KickOff, Pass, Carry, Tackle, Foul, Yellow, Red, Shot, Save, Post, Miss, Goal,
    Corner, Offside, Injury, HydrationBreak, HalfTime, SecondHalf, FullTime,
    ExtraTimeStart, ShootoutStart, PenaltyGoal, PenaltyMiss, End,
    /// <summary>Penal cobrado DURANTE el partido (distinto de la tanda final).</summary>
    PenaltyAwarded,
    /// <summary>Saque de banda: la pelota salió por el costado.</summary>
    ThrowIn,
    /// <summary>Tiro libre DIRECTO: se remata al arco por encima de la barrera.</summary>
    FreeKick,
    /// <summary>Tiro libre INDIRECTO: no se puede rematar, hay que tocarla antes.</summary>
    FreeKickIndirect
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
    public string Text { get; set; } = "";    // línea del relato (español, fallback)
    // Datos neutros para reconstruir el relato en cualquier idioma (nombres y marcador no se traducen):
    public string Actor { get; set; } = "";   // jugador/equipo principal del evento
    public string Target { get; set; } = "";  // jugador/equipo secundario (víctima, equipo, arquero…)
    /// <summary>Id del jugador protagonista, para atribuirle la estadística.</summary>
    public string ActorId { get; set; } = "";
    /// <summary>Id del jugador secundario (p. ej. el que sufre la falta o la lesión).</summary>
    public string TargetId { get; set; } = "";
    public int ScoreH { get; set; } = -1;      // marcador local al momento (o -1 si no aplica)
    public int ScoreA { get; set; } = -1;      // marcador visitante al momento
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

    /// <summary>
    /// Sólo cuando se simuló el PRIMER TIEMPO: con esto se reanuda el partido
    /// después de los cambios del entretiempo. null = el partido está completo.
    /// </summary>
    public MatchState? Resume { get; set; }
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


/// <summary>Qué parte del partido simular.</summary>
public enum MatchHalf { Full, First, Second }

/// <summary>
/// Lo que hay que guardar del entretiempo para poder seguir el partido despues:
/// el marcador, las estadisticas, los goles, la posesion y los eventos ya
/// jugados. Sin esto el segundo tiempo arrancaria de cero.
/// </summary>
public sealed class MatchState
{
    public List<SimEvent> Events { get; set; } = new();
    public MatchStats Stats { get; set; } = new();
    public List<Goal> Goals { get; set; } = new();
    public int ScoreHome { get; set; }
    public int ScoreAway { get; set; }
    public double PossHomeSecs { get; set; }
    public double PossAwaySecs { get; set; }
    /// <summary>Semilla para que el 2T siga siendo aleatorio pero reproducible.</summary>
    public int Seed { get; set; }
}
