namespace OnceDeOro.Models;

/// <summary>Foto de un rival dentro del calendario de un torneo (serializable).</summary>
public sealed class RivalSnapshot
{
    public string Name { get; set; } = "";
    public string Flag { get; set; } = "";
    public int Strength { get; set; }
}

/// <summary>Estado serializable de una participación en un torneo.</summary>
public sealed class RunState
{
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

    // Estadísticas de carrera
    public int MatchesPlayed { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int BestRatingReached { get; set; }
    public List<string> Honours { get; set; } = new();   // trofeos ganados
    public bool AchievedSevenZero { get; set; }           // logro legendario

    public RunState? Run { get; set; }

    public bool HasSeenIntro { get; set; }
}
