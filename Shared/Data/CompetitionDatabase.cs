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
            Name = "Copa Regional",
            Subtitle = "El primer trofeo · rivales accesibles",
            Emblem = "🥉",
            Accent = "#7fd1b9",
            Tier = 1,
            RecommendedStrength = 66,
            EntryFee = 0,
            ChampionPrize = 60,
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
    };

    public static Competition ById(string id) => All.First(c => c.Id == id);
}
