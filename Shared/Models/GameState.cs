namespace OnceDeOro.Models;

/// <summary>Foto de un rival dentro del calendario de un torneo (serializable).</summary>
public sealed class RivalSnapshot
{
    public string Name { get; set; } = "";
    public string Flag { get; set; } = "";
    public int Strength { get; set; }
}

/// <summary>
/// Un jugador que creaste vos en la academia y que sube de nivel entrenando y jugando.
/// Es el sumidero de dinero del juego avanzado: entrenar cuesta cada vez más caro.
/// </summary>
public sealed class AcademyPlayer
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Nation { get; set; } = "";
    public string Flag { get; set; } = "🎓";
    public Position Pos { get; set; }
    public int Rating { get; set; } = 55;

    /// <summary>Experiencia acumulada hacia el próximo punto de fuerza.</summary>
    public int Xp { get; set; }
    public int Sessions { get; set; }
    public int Matches { get; set; }
}

/// <summary>
/// Estado físico e historial de UN jugador tuyo: lo que se acumula partido a partido.
/// </summary>
public sealed class PlayerCondition
{
    /// <summary>0 = entero, 100 = fundido. Resta rendimiento y aumenta el riesgo de lesión.</summary>
    public int Fatigue { get; set; }

    /// <summary>Partidos que le faltan para volver. 0 = disponible.</summary>
    public int OutMatches { get; set; }

    // ---- Estadísticas acumuladas con vos ----
    public int Matches { get; set; }
    public int Goals { get; set; }
    public int Yellow { get; set; }
    public int Red { get; set; }
    public int Injuries { get; set; }

    public bool Injured => OutMatches > 0;
}

/// <summary>Una fila de la tabla de posiciones de una liga.</summary>
public sealed class TableRow
{
    public string Name { get; set; } = "";
    public string Flag { get; set; } = "";
    public int Strength { get; set; }
    public bool IsMe { get; set; }
    public int Played { get; set; }
    public int Won { get; set; }
    public int Drawn { get; set; }
    public int Lost { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }

    public int Points => Won * 3 + Drawn;
    public int Diff => GoalsFor - GoalsAgainst;
}

/// <summary>Estado serializable de una participación en un torneo.</summary>
public sealed class RunState
{
    /// <summary>Tabla de posiciones (sólo en ligas). Incluye tu equipo y a los rivales.</summary>
    public List<TableRow> Table { get; set; } = new();

    public string CompId { get; set; } = "";
    public int Stage { get; set; }
    public int GroupPoints { get; set; }
    public int GroupPlayed { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public bool Eliminated { get; set; }
    public bool Champion { get; set; }
    public bool CleanRun { get; set; } = true;
    public bool PerfectRun { get; set; } = true;
    public int MoneyWon { get; set; }
    public List<RivalSnapshot> Schedule { get; set; } = new();
    public List<string> Timeline { get; set; } = new();
}

/// <summary>Todo el progreso persistente del jugador. Se guarda en localStorage.</summary>
public sealed class GameState
{
    public int Money { get; set; } = 200;
    public string ClubName { get; set; } = "Once de Oro FC";
    public string Primary { get; set; } = "#f5c542";
    public string FormationName { get; set; } = "4-3-3";
    public TeamStyle Style { get; set; } = TeamStyle.Equilibrado;

    public List<string> OwnedIds { get; set; } = new();
    public List<string> StartingIds { get; set; } = new();

    /// <summary>Jugadores creados y entrenados por vos en la academia.</summary>
    public List<AcademyPlayer> Academy { get; set; } = new();

    /// <summary>Cansancio, lesiones y estadísticas de cada jugador, por Id.</summary>
    public Dictionary<string, PlayerCondition> Conditions { get; set; } = new();

    // Estadísticas de carrera
    public int MatchesPlayed { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int BestRatingReached { get; set; }
    public List<string> Honours { get; set; } = new();   // trofeos ganados
    public List<string> History { get; set; } = new();    // historial de torneos jugados
    public bool AchievedSevenZero { get; set; }           // logro legendario

    public RunState? Run { get; set; }

    public bool HasSeenIntro { get; set; }
}
