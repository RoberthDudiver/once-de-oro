using OnceDeOro.Models;

namespace OnceDeOro.Data;

/// <summary>Los cuatro torneos, de menor a mayor prestigio.</summary>
public static class CompetitionDatabase
{
    private static RivalTeam R(string n, string f, int s) => new(n, f, s);

    public static readonly IReadOnlyList<Competition> All = new List<Competition>
    {
        new()
        {
            Id = "regional",
            Kind = CompetitionKind.Continental,
            Name = "Copa Regional",
            Subtitle = "El primer trofeo · rivales accesibles",
            Emblem = "🥉",
            Accent = "#7fd1b9",
            Tier = 1,
            RecommendedStrength = 66,
            EntryFee = 0,
            ChampionPrize = 900,
            KnockoutRounds = new[] { "Cuartos", "Semifinal", "Final" },
            Rivals = new[]
            {
                R("Río Verde", "🟢", 60), R("Puerto Azul", "🔵", 63), R("Sierra FC", "⚪", 65),
                R("Costa Sur", "🟡", 62), R("Real Llanura", "🔴", 67), R("Deportivo Norte", "⚫", 64),
                R("Unión Valle", "🟠", 66), R("Atlético Lago", "🟣", 68),
            },
        },
        new()
        {
            Id = "america",
            Kind = CompetitionKind.Selecciones,
            Name = "Copa América",
            Subtitle = "El continente sudamericano en juego",
            Emblem = "🏆",
            Accent = "#4ea3ff",
            Tier = 2,
            RecommendedStrength = 76,
            EntryFee = 8,
            ChampionPrize = 160,
            KnockoutRounds = new[] { "Cuartos", "Semifinal", "Final" },
            Rivals = new[]
            {
                R("Brasil", "🇧🇷", 84), R("Argentina", "🇦🇷", 85), R("Uruguay", "🇺🇾", 79),
                R("Colombia", "🇨🇴", 77), R("Chile", "🇨🇱", 76), R("Paraguay", "🇵🇾", 72),
                R("Perú", "🇵🇪", 73), R("Ecuador", "🇪🇨", 74), R("Venezuela", "🇻🇪", 74),
            },
        },
        new()
        {
            Id = "champions",
            Kind = CompetitionKind.Continental,
            Name = "Champions League",
            Subtitle = "La élite europea bajo las luces",
            Emblem = "⭐",
            Accent = "#b98bff",
            Tier = 3,
            RecommendedStrength = 84,
            EntryFee = 20,
            ChampionPrize = 320,
            KnockoutRounds = new[] { "Octavos", "Cuartos", "Semifinal", "Final" },
            Rivals = new[]
            {
                R("Los Merengues", "⚪", 88), R("Blaugrana FC", "🔵", 87), R("Baviera 09", "🔴", 86),
                R("City Sky", "🩵", 87), R("Rossoneri", "🔴", 83), R("Les Parisiens", "🔵", 85),
                R("Reds Anfield", "🔴", 85), R("Old Trafford XI", "🔴", 82),
                R("Turín Vecchia", "⚫", 83), R("Colchoneros", "🔴", 82),
            },
        },
        new()
        {
            Id = "mundial",
            Kind = CompetitionKind.Selecciones,
            Name = "Copa del Mundo",
            Subtitle = "La cima · aquí se forja el 7–0 perfecto",
            Emblem = "🌍",
            Accent = "#f5c542",
            Tier = 4,
            RecommendedStrength = 90,
            EntryFee = 40,
            ChampionPrize = 640,
            KnockoutRounds = new[] { "Octavos", "Cuartos", "Semifinal", "Final" },
            Rivals = new[]
            {
                R("Brasil", "🇧🇷", 90), R("Argentina", "🇦🇷", 91), R("Francia", "🇫🇷", 90),
                R("Alemania", "🇩🇪", 89), R("España", "🇪🇸", 89), R("Inglaterra", "🏴󠁧󠁢󠁥󠁮󠁧󠁿", 88),
                R("Países Bajos", "🇳🇱", 87), R("Italia", "🇮🇹", 87), R("Portugal", "🇵🇹", 88),
                R("Bélgica", "🇧🇪", 86), R("Croacia", "🇭🇷", 85), R("Uruguay", "🇺🇾", 84),
            },
        },

        // ---------------------------------------------------------- LIGAS NACIONALES
        // Temporada larga: todos contra todos, tabla de posiciones, cobra el que termina arriba.
        League("liga-arg", "Liga Argentina", "Temporada completa en el fútbol argentino", "🇦🇷", "#7fd1ff", 1, 68, 4, 90, new[]
        {
            R("River", "🔴", 78), R("Boca", "🔵", 78), R("Racing", "🩵", 74), R("Independiente", "🔴", 72),
            R("San Lorenzo", "🔵", 71), R("Vélez", "⚪", 73), R("Estudiantes", "🔴", 72), R("Talleres", "🔵", 71),
            R("Rosario Central", "🟡", 70), R("Newell's", "🔴", 70),
        }),
        League("liga-bra", "Brasileirão", "El campeonato más físico de Sudamérica", "🇧🇷", "#37d67a", 2, 76, 8, 150, new[]
        {
            R("Flamengo", "🔴", 82), R("Palmeiras", "🟢", 82), R("São Paulo", "🔴", 78), R("Corinthians", "⚫", 77),
            R("Grêmio", "🔵", 76), R("Internacional", "🔴", 75), R("Atlético Mineiro", "⚫", 77), R("Fluminense", "🟢", 75),
            R("Santos", "⚪", 74), R("Botafogo", "⚫", 76),
        }),
        League("liga-esp", "La Liga", "Técnica y posesión al máximo nivel", "🇪🇸", "#f5c542", 3, 84, 18, 300, new[]
        {
            R("Los Merengues", "⚪", 89), R("Blaugrana FC", "🔵", 88), R("Colchoneros", "🔴", 85),
            R("Sevilla FC", "⚪", 80), R("Real Sociedad", "🔵", 79), R("Villarreal", "🟡", 79),
            R("Athletic", "🔴", 78), R("Betis", "🟢", 78), R("Valencia", "🟠", 77), R("Girona", "🔴", 77),
        }),
        League("liga-ita", "Serie A", "Táctica italiana: el fútbol más cerrado", "🇮🇹", "#4ea3ff", 3, 83, 16, 280, new[]
        {
            R("Nerazzurri", "🔵", 87), R("Rossoneri", "🔴", 85), R("Turín Vecchia", "⚫", 86),
            R("Partenopei", "🩵", 85), R("Giallorossi", "🟡", 82), R("Biancocelesti", "🩵", 81),
            R("Viola", "🟣", 80), R("Bergamaschi", "🔵", 83), R("Rossoblu", "🔴", 79), R("Granata", "🟤", 78),
        }),
        League("liga-eng", "Premier League", "La liga más competitiva del mundo", "🏴󠁧󠁢󠁥󠁮󠁧󠁿", "#b98bff", 4, 87, 25, 420, new[]
        {
            R("City Sky", "🩵", 89), R("Reds Anfield", "🔴", 88), R("Gunners N5", "🔴", 87),
            R("Old Trafford XI", "🔴", 84), R("Blues West", "🔵", 84), R("Spurs N17", "⚪", 83),
            R("Newcastle", "⚫", 82), R("Aston Villa", "🟣", 81), R("Brighton", "🔵", 79), R("West Ham", "🟤", 79),
        }),

        // ---------------------------------------------------------- COPAS NACIONALES (eliminación directa)
        Cup("copa-arg", "Copa Argentina", "Eliminación directa: no se perdona nada", "🏵️", "#7fd1ff", 1, 70, 3, 80,
            new[] { "Dieciseisavos", "Octavos", "Cuartos", "Semifinal", "Final" }, new[]
        {
            R("Defensa y Justicia", "🟡", 68), R("Lanús", "🔴", 70), R("Huracán", "🔴", 69),
            R("Argentinos", "🔴", 70), R("Tigre", "🔵", 67), R("Banfield", "🟢", 68),
            R("Racing", "🩵", 74), R("River", "🔴", 78), R("Boca", "🔵", 78), R("Vélez", "⚪", 73),
        }),
        Cup("copa-esp", "Copa del Rey", "El torneo de las sorpresas", "👑", "#f5c542", 3, 82, 12, 200,
            new[] { "Octavos", "Cuartos", "Semifinal", "Final" }, new[]
        {
            R("Osasuna", "🔴", 76), R("Celta", "🔵", 76), R("Getafe", "🔵", 75), R("Mallorca", "🔴", 75),
            R("Sevilla FC", "⚪", 80), R("Colchoneros", "🔴", 85), R("Blaugrana FC", "🔵", 88), R("Los Merengues", "⚪", 89),
        }),
        Cup("fa-cup", "FA Cup", "La copa más antigua del fútbol", "🦁", "#b98bff", 4, 85, 15, 260,
            new[] { "Octavos", "Cuartos", "Semifinal", "Final" }, new[]
        {
            R("Everton", "🔵", 77), R("Crystal Palace", "🔴", 76), R("Fulham", "⚪", 77), R("Wolves", "🟠", 76),
            R("Blues West", "🔵", 84), R("Gunners N5", "🔴", 87), R("City Sky", "🩵", 89), R("Reds Anfield", "🔴", 88),
        }),

        // ---------------------------------------------------------- CONTINENTALES
        new()
        {
            Id = "libertadores",
            Name = "Copa Libertadores",
            Subtitle = "La gloria eterna de Sudamérica",
            Emblem = "🏅",
            Accent = "#37d67a",
            Tier = 3,
            Kind = CompetitionKind.Continental,
            RecommendedStrength = 80,
            EntryFee = 14,
            ChampionPrize = 260,
            KnockoutRounds = new[] { "Octavos", "Cuartos", "Semifinal", "Final" },
            Rivals = new[]
            {
                R("Flamengo", "🔴", 82), R("Palmeiras", "🟢", 82), R("River", "🔴", 80), R("Boca", "🔵", 80),
                R("Atlético Mineiro", "⚫", 78), R("Peñarol", "🟡", 74), R("Nacional", "⚪", 74),
                R("Colo-Colo", "⚪", 73), R("Olimpia", "⚪", 73), R("Caracas FC", "🔴", 70),
            },
        },
        new()
        {
            Id = "europa",
            Name = "Europa League",
            Subtitle = "El camino alternativo a la gloria europea",
            Emblem = "🌍",
            Accent = "#ff9a3d",
            Tier = 2,
            Kind = CompetitionKind.Continental,
            RecommendedStrength = 78,
            EntryFee = 10,
            ChampionPrize = 180,
            KnockoutRounds = new[] { "Octavos", "Cuartos", "Semifinal", "Final" },
            Rivals = new[]
            {
                R("Sevilla FC", "⚪", 80), R("Roma", "🟡", 80), R("Leverkusen", "🔴", 82), R("Villarreal", "🟡", 79),
                R("Ajax", "🔴", 78), R("Benfica", "🔴", 79), R("Feyenoord", "🔴", 77), R("Lazio", "🩵", 78),
                R("Rangers", "🔵", 74), R("Betis", "🟢", 78),
            },
        },

        // ---------------------------------------------------------- SELECCIONES
        new()
        {
            Id = "eurocopa",
            Name = "Eurocopa",
            Subtitle = "El torneo de selecciones más parejo",
            Emblem = "⭐",
            Accent = "#4ea3ff",
            Tier = 3,
            Kind = CompetitionKind.Selecciones,
            RecommendedStrength = 85,
            EntryFee = 22,
            ChampionPrize = 340,
            KnockoutRounds = new[] { "Octavos", "Cuartos", "Semifinal", "Final" },
            Rivals = new[]
            {
                R("Francia", "🇫🇷", 90), R("España", "🇪🇸", 89), R("Alemania", "🇩🇪", 89), R("Inglaterra", "🏴󠁧󠁢󠁥󠁮󠁧󠁿", 88),
                R("Portugal", "🇵🇹", 88), R("Italia", "🇮🇹", 87), R("Países Bajos", "🇳🇱", 87), R("Bélgica", "🇧🇪", 86),
                R("Croacia", "🇭🇷", 85), R("Dinamarca", "🇩🇰", 82), R("Suiza", "🇨🇭", 81), R("Austria", "🇦🇹", 80),
            },
        },
    };

    /// <summary>Atajo para definir una LIGA (todos contra todos con tabla).</summary>
    private static Competition League(string id, string name, string subtitle, string emblem, string accent,
                                      int tier, int rec, int fee, int prize, RivalTeam[] rivals) => new()
    {
        Id = id, Name = name, Subtitle = subtitle, Emblem = emblem, Accent = accent,
        Tier = tier, Kind = CompetitionKind.Liga, Format = CompetitionFormat.League,
        RecommendedStrength = rec, EntryFee = fee, ChampionPrize = prize,
        LeagueRounds = rivals.Length,
        KnockoutRounds = Array.Empty<string>(),
        Rivals = rivals,
    };

    /// <summary>Atajo para definir una COPA nacional (eliminación directa desde el arranque).</summary>
    private static Competition Cup(string id, string name, string subtitle, string emblem, string accent,
                                   int tier, int rec, int fee, int prize, string[] rounds, RivalTeam[] rivals) => new()
    {
        Id = id, Name = name, Subtitle = subtitle, Emblem = emblem, Accent = accent,
        Tier = tier, Kind = CompetitionKind.CopaNacional, Format = CompetitionFormat.Knockout,
        RecommendedStrength = rec, EntryFee = fee, ChampionPrize = prize,
        KnockoutRounds = rounds,
        Rivals = rivals,
    };

    public static Competition ById(string id) => All.First(c => c.Id == id);
}
