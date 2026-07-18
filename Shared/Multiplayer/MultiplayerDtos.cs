namespace OnceDeOro.Multiplayer;

/// <summary>Cómo consigue su equipo cada jugador en un duelo online.</summary>
public enum TeamMode { Draft, Career }

/// <summary>Formato de la contienda online.</summary>
public enum MatchFormat { Single, BestOf3, MiniTournament }

/// <summary>Configuración de una sala, elegida por quien la crea.</summary>
public sealed class RoomConfig
{
    public TeamMode TeamMode { get; set; } = TeamMode.Draft;
    public MatchFormat Format { get; set; } = MatchFormat.Single;
    public int Budget { get; set; } = 500;    // presupuesto compartido (modo Draft)
    public int Capacity { get; set; } = 2;      // 2 (duelo) o hasta 4 (mini-torneo)
}

/// <summary>Un jugador dentro de un equipo enviado (autosuficiente, sin lookup).</summary>
public sealed class PlayerLite
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Flag { get; set; } = "";
    public string Pos { get; set; } = "";   // "GK" | "DEF" | "MID" | "FWD"
    public int Rating { get; set; }
}

/// <summary>El XI que un jugador envía al servidor.</summary>
public sealed class TeamDto
{
    public string Formation { get; set; } = "4-3-3";
    public string Style { get; set; } = "Equilibrado";
    public List<PlayerLite> Starters { get; set; } = new();
}

public sealed class RoomMemberDto
{
    public string Id { get; set; } = "";      // connection id
    public string Name { get; set; } = "";
    public bool HasTeam { get; set; }
    public bool Ready { get; set; }
    public bool Connected { get; set; } = true;
    public int SeriesWins { get; set; }        // partidos ganados en la serie/bracket
    public bool Eliminated { get; set; }
}

/// <summary>Estado completo de la sala que se sincroniza a todos.</summary>
public sealed class RoomStateDto
{
    public string Code { get; set; } = "";
    public string HostId { get; set; } = "";
    public RoomConfig Config { get; set; } = new();
    public string Phase { get; set; } = "lobby";  // lobby | building | playing | result | done
    public List<RoomMemberDto> Members { get; set; } = new();
    public string Title { get; set; } = "";         // p.ej. "Final", "Mejor de 3 (1-0)"
    public string? Pair0 { get; set; }              // ids del enfrentamiento actual
    public string? Pair1 { get; set; }

    /// <summary>
    /// Ids de jugadores ya fichados por algún rival: nadie más puede usarlos.
    /// El cliente los muestra bloqueados y el servidor además lo hace cumplir.
    /// </summary>
    public List<string> TakenPlayerIds { get; set; } = new();
}

/// <summary>Datos de arranque de un partido dentro del bracket/serie.</summary>
public sealed class MatchStartDto
{
    public string HomeId { get; set; } = "";
    public string AwayId { get; set; } = "";
    public string HomeName { get; set; } = "";
    public string AwayName { get; set; } = "";
    public string HomeFlag { get; set; } = "⚽";
    public string AwayFlag { get; set; } = "⚽";
    public int HomePower { get; set; }
    public int AwayPower { get; set; }
    public string Title { get; set; } = "";
    public int HomeSeriesWins { get; set; }
    public int AwaySeriesWins { get; set; }
}

/// <summary>Un evento (gol) durante la simulación en vivo.</summary>
public sealed class GoalDto
{
    public int Minute { get; set; }
    public bool HomeSide { get; set; }   // Home = Pair0
    public string Scorer { get; set; } = "";
}

/// <summary>Resultado final de un partido online.</summary>
public sealed class MatchResultDto
{
    public string HomeName { get; set; } = "";
    public string AwayName { get; set; } = "";
    public string HomeFlag { get; set; } = "";
    public string AwayFlag { get; set; } = "";
    public int HomeGoals { get; set; }
    public int AwayGoals { get; set; }
    public bool Penalties { get; set; }
    public int HomePens { get; set; }
    public int AwayPens { get; set; }
    public string WinnerId { get; set; } = "";
    public List<GoalDto> Goals { get; set; } = new();
}
