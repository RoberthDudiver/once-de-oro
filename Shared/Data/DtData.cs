using OnceDeOro.Models;

namespace OnceDeOro.Data;

/// <summary>Clubes dirigibles del Modo DT, agrupados por nivel.</summary>
public static class DtData
{
    private static DtClub C(string name, string emblem, string country, string league, int level, int str, int budget)
        => new() { Name = name, Emblem = emblem, Country = country, League = league, Level = level, Strength = str, BudgetM = budget };

    public static readonly IReadOnlyList<DtClub> Clubs = new List<DtClub>
    {
        // -------- Nivel 1: Pequeño (pelea por no descender / crecer) --------
        C("Defensores del Sur", "🟢", "Argentina", "Primera Nacional", 1, 60, 3),
        C("Atlético Porteño", "🔵", "Argentina", "Primera Nacional", 1, 62, 4),
        C("Unión del Valle", "🟠", "Argentina", "Primera Nacional", 1, 61, 3),
        C("Deportivo Costa", "🟡", "Uruguay", "Primera Uruguay", 1, 63, 4),
        C("Real Montaña", "⚪", "Colombia", "Liga Colombia", 1, 64, 5),

        // -------- Nivel 2: Mediano (copas / puestos internacionales) --------
        C("Racing", "🩵", "Argentina", "Liga Profesional", 2, 72, 12),
        C("Independiente", "🔴", "Argentina", "Liga Profesional", 2, 71, 11),
        C("San Lorenzo", "🔵", "Argentina", "Liga Profesional", 2, 70, 10),
        C("Fluminense", "🟢", "Brasil", "Brasileirão", 2, 73, 16),
        C("Betis", "🟢", "España", "LaLiga", 2, 74, 22),
        C("Villarreal", "🟡", "España", "LaLiga", 2, 75, 26),

        // -------- Nivel 3: Grande (exige títulos) --------
        C("River", "🔴", "Argentina", "Liga Profesional", 3, 80, 30),
        C("Boca", "🔵", "Argentina", "Liga Profesional", 3, 80, 30),
        C("Flamengo", "🔴", "Brasil", "Brasileirão", 3, 82, 40),
        C("Sevilla FC", "⚪", "España", "LaLiga", 3, 81, 45),
        C("Roma", "🟡", "Italia", "Serie A", 3, 82, 55),
        C("Atalanta", "🔵", "Italia", "Serie A", 3, 83, 60),

        // -------- Nivel 4: Gigante (obligación de ganar todo) --------
        C("Los Merengues", "⚪", "España", "LaLiga", 4, 89, 220),
        C("Blaugrana FC", "🔵", "España", "LaLiga", 4, 88, 200),
        C("Baviera 09", "🔴", "Alemania", "Bundesliga", 4, 88, 190),
        C("City Sky", "🩵", "Inglaterra", "Premier League", 4, 89, 240),
        C("Les Parisiens", "🔵", "Francia", "Ligue 1", 4, 87, 210),
        C("Reds Anfield", "🔴", "Inglaterra", "Premier League", 4, 88, 200),
    };

    public static IEnumerable<DtClub> ByLevel(int level) => Clubs.Where(c => c.Level == level);

    public static string LevelName(int level) => level switch
    {
        1 => "Club pequeño", 2 => "Club mediano", 3 => "Club grande", _ => "Club gigante"
    };
}
