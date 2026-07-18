namespace OnceDeOro.Models;

/// <summary>Un rival controlado por la IA dentro de un torneo.</summary>
public sealed record RivalTeam(string Name, string Flag, int Strength);

/// <summary>
/// Definición de un torneo: prestigio, formato (grupos + eliminatorias),
/// pool de rivales y tabla de premios por ronda.
/// </summary>
public sealed class Competition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Subtitle { get; init; }
    public required string Emblem { get; init; }        // emoji/insignia
    public required string Accent { get; init; }         // color acento
    public int Tier { get; init; }                       // 1 = inicial … 4 = Mundial
    public int RecommendedStrength { get; init; }
    public int EntryFee { get; init; }                   // costo de inscripción (M)
    public int ChampionPrize { get; init; }              // premio al campeón (M)
    public required RivalTeam[] Rivals { get; init; }

    /// <summary>Nombres de las rondas eliminatorias (tras la fase de grupos).</summary>
    public required string[] KnockoutRounds { get; init; }

    /// <summary>Premio por superar una ronda (índice = etapa global).</summary>
    public int PrizePerRound => Math.Max(1, ChampionPrize / 12);
}

/// <summary>Estado de una participación en curso en un torneo.</summary>
public sealed class TournamentRun
{
    public required Competition Comp { get; init; }
    public int Stage { get; set; }                       // 0..N: índice de la etapa actual
    public int GroupPoints { get; set; }
    public int GroupPlayed { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public bool Eliminated { get; set; }
    public bool Champion { get; set; }
    public bool CleanRun { get; set; } = true;           // sin recibir goles (rumbo al 7-0)
    public bool PerfectRun { get; set; } = true;         // todos 7-0 exactos (marca legendaria)
    public int MoneyWon { get; set; }
    public List<string> Timeline { get; set; } = new();  // resumen de resultados

    // 3 partidos de grupo + eliminatorias
    public int GroupMatches => 3;
    public int TotalStages => GroupMatches + Comp.KnockoutRounds.Length;

    public bool InGroupPhase => Stage < GroupMatches;
    public string CurrentStageName =>
        InGroupPhase ? $"Fase de grupos · J{Stage + 1}"
                     : Comp.KnockoutRounds[Stage - GroupMatches];

    public bool Finished => Eliminated || Champion;
}
