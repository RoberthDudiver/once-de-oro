namespace OnceDeOro.Models;

/// <summary>Posición de un jugador en el campo.</summary>
public enum Position { GK, DEF, MID, FWD }

/// <summary>Estilo de juego del equipo — sesga el motor de simulación.</summary>
public enum TeamStyle { Defensivo, Equilibrado, Ofensivo }

public static class PositionExtensions
{
    public static string Label(this Position p) => p switch
    {
        Position.GK => "POR",
        Position.DEF => "DEF",
        Position.MID => "MED",
        Position.FWD => "DEL",
        _ => "?"
    };

    public static string Full(this Position p) => p switch
    {
        Position.GK => "Portero",
        Position.DEF => "Defensa",
        Position.MID => "Mediocampo",
        Position.FWD => "Delantero",
        _ => "?"
    };
}

/// <summary>Un jugador fichable. La fuerza (Rating) manda; el precio deriva de ella.</summary>
public sealed class Player
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Nation { get; init; }       // nombre de la selección
    public required string Flag { get; init; }          // emoji bandera
    public required Position Pos { get; init; }
    public required int Rating { get; init; }           // 1-99 (fuerza)
    public bool IsLegend { get; init; }                 // sello dorado
    public int Era { get; init; }                       // año/mundial de referencia

    /// <summary>Valor de mercado en millones, derivado de la fuerza y el aura de leyenda.</summary>
    public int Value
    {
        get
        {
            // Curva exponencial calibrada:  60→~3M · 70→~23M · 80→~77M · 90→~178M · 95→~254M
            double baseVal = 0.0034 * Math.Pow(Math.Max(1, Rating - 50), 2.95);
            if (IsLegend) baseVal *= 1.3;
            return Math.Max(1, (int)Math.Round(baseVal));
        }
    }
}

/// <summary>Definición de una formación: cuántos jugadores por línea.</summary>
public sealed record Formation(string Name, int Def, int Mid, int Fwd)
{
    public int Gk => 1;
    public int Total => Gk + Def + Mid + Fwd;

    public static readonly Formation F433 = new("4-3-3", 4, 3, 3);
    public static readonly Formation F442 = new("4-4-2", 4, 4, 2);
    public static readonly Formation F352 = new("3-5-2", 3, 5, 2);
    public static readonly Formation F532 = new("5-3-2", 5, 3, 2);
    public static readonly Formation F4231 = new("4-2-3-1", 4, 5, 1);

    public static readonly Formation[] All = { F433, F442, F352, F532, F4231 };

    /// <summary>Cuántos titulares de una posición dada requiere esta formación.</summary>
    public int SlotsFor(Position pos) => pos switch
    {
        Position.GK => Gk,
        Position.DEF => Def,
        Position.MID => Mid,
        Position.FWD => Fwd,
        _ => 0
    };
}

/// <summary>El equipo del jugador: plantel comprado, XI titular, formación, estilo.</summary>
public sealed class Squad
{
    public string ClubName { get; set; } = "Once de Oro FC";
    public string Primary { get; set; } = "#f5c542";   // color principal (escudo)
    public List<Player> Owned { get; set; } = new();
    public List<string> StartingIds { get; set; } = new();  // ids titulares (máx 11)
    public string FormationName { get; set; } = Formation.F433.Name;
    public TeamStyle Style { get; set; } = TeamStyle.Equilibrado;

    public Formation Formation =>
        Formation.All.FirstOrDefault(f => f.Name == FormationName) ?? Formation.F433;

    public IEnumerable<Player> Starters =>
        StartingIds.Select(id => Owned.FirstOrDefault(p => p.Id == id))
                   .Where(p => p is not null)!.Cast<Player>();

    public bool IsValidXI
    {
        get
        {
            var starters = Starters.ToList();
            if (starters.Count != 11) return false;
            var f = Formation;
            foreach (var pos in Enum.GetValues<Position>())
                if (starters.Count(p => p.Pos == pos) != f.SlotsFor(pos))
                    return false;
            return true;
        }
    }
}

/// <summary>Un gol dentro de un partido.</summary>
public sealed record Goal(int Minute, string Scorer, bool HomeSide, bool IsPenalty = false);

/// <summary>Resultado completo de un partido simulado.</summary>
public sealed class MatchResult
{
    public required string HomeName { get; init; }
    public required string AwayName { get; init; }
    public required string HomeFlag { get; init; }
    public required string AwayFlag { get; init; }
    public int HomeGoals { get; set; }
    public int AwayGoals { get; set; }
    public List<Goal> Goals { get; init; } = new();
    public bool WentToPenalties { get; set; }
    public int HomePens { get; set; }
    public int AwayPens { get; set; }

    public bool HomeWon => WentToPenalties ? HomePens > AwayPens : HomeGoals > AwayGoals;
    public bool CleanSheetForHome => AwayGoals == 0;
}
