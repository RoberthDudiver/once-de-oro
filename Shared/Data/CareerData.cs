using OnceDeOro.Models;

namespace OnceDeOro.Data;

/// <summary>Naciones y clubes (por nivel) para el simulador de carrera.</summary>
public static class CareerData
{
    /// <summary>Nación con su código de 3 letras y su "tier" (1 = potencia … 4 = modesta).</summary>
    public sealed record Nation(string Name, string Code, int Tier);

    public static readonly IReadOnlyList<Nation> Nations = new List<Nation>
    {
        new("Argentina","ARG",1), new("Brasil","BRA",1), new("Francia","FRA",1), new("Alemania","ALE",1),
        new("España","ESP",1), new("Inglaterra","ING",1), new("Portugal","POR",1), new("Países Bajos","NED",1),
        new("Italia","ITA",1), new("Bélgica","BEL",1), new("Uruguay","URU",2), new("Croacia","CRO",2),
        new("Colombia","COL",2), new("México","MEX",2), new("Estados Unidos","USA",2), new("Chile","CHI",2),
        new("Dinamarca","DIN",2), new("Suiza","SUI",2), new("Serbia","SRB",2), new("Marruecos","MAR",2),
        new("Japón","JPN",2), new("Corea","KOR",2), new("Senegal","SEN",2), new("Ecuador","ECU",3),
        new("Perú","PER",3), new("Paraguay","PAR",3), new("Venezuela","VEN",3), new("Nigeria","NGA",3),
        new("Ghana","GHA",3), new("Costa de Marfil","CIV",3), new("Polonia","POL",3), new("Austria","AUT",3),
        new("Escocia","SCO",3), new("Gales","WAL",3), new("Canadá","CAN",3), new("Australia","AUS",3),
        new("Egipto","EGY",3), new("Argelia","ALG",3), new("Bolivia","BOL",4), new("Honduras","HON",4),
    };

    public static Nation NationByName(string name) =>
        Nations.FirstOrDefault(n => n.Name == name) ?? Nations[0];

    // ---- Clubes por nivel (1 = ascenso/cantera … 5 = elite mundial) ----
    private static readonly Dictionary<int, string[]> Clubs = new()
    {
        [1] = new[] { "Juventud Unida", "Deportivo Norte", "Atlético Sur", "San Martín", "Sportivo Central",
                      "Defensores", "Racing B", "El Porvenir", "Argentino", "Talleres (RdE)" },
        [2] = new[] { "Central Norte", "Almirante Brown", "Patronato", "Gimnasia (J)", "Chacarita",
                      "Ferro", "Quilmes", "San Telmo", "Temperley", "Deportivo Morón" },
        [3] = new[] { "River", "Boca", "Racing", "Independiente", "San Lorenzo", "Vélez",
                      "Estudiantes", "Flamengo", "Palmeiras", "Peñarol", "Nacional", "Colo-Colo" },
        [4] = new[] { "Sevilla FC", "Roma", "Ajax", "Benfica", "Villarreal", "Porto", "Lazio",
                      "Leverkusen", "Atalanta", "Marsella", "Newcastle", "Aston Villa" },
        [5] = new[] { "Los Merengues", "Blaugrana FC", "Baviera 09", "City Sky", "Les Parisiens",
                      "Reds Anfield", "Turín Vecchia", "Old Trafford XI", "Colchoneros", "Nerazzurri" },
    };

    public static string[] LeagueForTier(int tier) => tier switch
    {
        1 => new[] { "Primera C", "Primera D", "Torneo Federal" },
        2 => new[] { "Primera Nacional", "Primera B" },
        3 => new[] { "Liga Profesional", "Brasileirão", "Liga Sudamericana" },
        4 => new[] { "Europa League", "Serie A", "LaLiga", "Ligue 1" },
        _ => new[] { "Champions League", "Premier League", "Elite Europea" },
    };

    /// <summary>Elige N clubes distintos de un nivel, sin repetir el actual.</summary>
    public static List<string> PickClubs(int tier, int n, string exclude, Random r)
    {
        tier = Math.Clamp(tier, 1, 5);
        var pool = Clubs[tier].Where(c => c != exclude).OrderBy(_ => r.Next()).ToList();
        return pool.Take(Math.Min(n, pool.Count)).ToList();
    }

    public static string League(int tier, Random r)
    {
        var ls = LeagueForTier(Math.Clamp(tier, 1, 5));
        return ls[r.Next(ls.Length)];
    }
}
