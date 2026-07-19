using OnceDeOro.Models;

namespace OnceDeOro.Data;

/// <summary>
/// Las versiones PRIME: el jugador en su mejor momento exacto. Son los únicos de
/// 100 a 109 del juego y NO están en el mercado a propósito — si se pudieran
/// comprar, bastaría con juntar plata y el tramo de 100+ dejaría de valer nada.
/// Sólo salen de las cajas.
///
/// El Pelé de 96 del mercado y el "Pelé (Prime)" de 108 son dos cartas distintas:
/// tener al primero no te da el segundo.
/// </summary>
public static class PrimeDatabase
{
    private static int _seq;
    private static Player P(string name, string year, string nation, string flag,
                            Position pos, int rating, int era)
        => new()
        {
            Id = $"prime{++_seq}",
            Name = $"{name} ({year})",
            Nation = nation,
            Flag = flag,
            Pos = pos,
            Rating = rating,
            Era = era,
            IsLegend = true,
        };

    public static readonly IReadOnlyList<Player> All = Build();

    private static List<Player> Build()
    {
        _seq = 0;
        const string BR = "🇧🇷", AR = "🇦🇷", DE = "🇩🇪", NL = "🇳🇱", IT = "🇮🇹",
                     FR = "🇫🇷", ES = "🇪🇸", PT = "🇵🇹", HU = "🇭🇺", RU = "🇷🇺",
                     HR = "🇭🇷", NO = "🇳🇴", EN = "🏴󠁧󠁢󠁥󠁮󠁧󠁿";

        return new List<Player>
        {
            // ---------- 109-107: el podio ----------
            P("Messi", "Prime 2012", "Argentina", AR, Position.FWD, 109, 2012),
            P("Maradona", "Prime 1986", "Argentina", AR, Position.MID, 108, 1986),
            P("Pelé", "Prime 1970", "Brasil", BR, Position.FWD, 108, 1970),
            P("Cristiano Ronaldo", "Prime 2014", "Portugal", PT, Position.FWD, 107, 2014),

            // ---------- 106-104 ----------
            P("Ronaldo Nazário", "Prime 1997", "Brasil", BR, Position.FWD, 106, 1997),
            P("Zidane", "Prime 1998", "Francia", FR, Position.MID, 105, 1998),
            P("Cruyff", "Prime 1974", "P. Bajos", NL, Position.FWD, 105, 1974),
            P("Ronaldinho", "Prime 2005", "Brasil", BR, Position.MID, 105, 2005),
            P("Di Stéfano", "Prime 1959", "Argentina", AR, Position.FWD, 104, 1959),
            P("Beckenbauer", "Prime 1974", "Alemania", DE, Position.DEF, 104, 1974),
            P("Yashin", "Prime 1963", "Rusia", RU, Position.GK, 104, 1963),
            P("Maldini", "Prime 1994", "Italia", IT, Position.DEF, 104, 1994),

            // ---------- 103-101 ----------
            P("Garrincha", "Prime 1962", "Brasil", BR, Position.FWD, 103, 1962),
            P("Puskás", "Prime 1954", "Hungría", HU, Position.FWD, 103, 1954),
            P("Platini", "Prime 1984", "Francia", FR, Position.MID, 103, 1984),
            P("Buffon", "Prime 2006", "Italia", IT, Position.GK, 103, 2006),
            P("Mbappé", "Prime 2022", "Francia", FR, Position.FWD, 103, 2022),
            P("Eusébio", "Prime 1966", "Portugal", PT, Position.FWD, 102, 1966),
            P("George Best", "Prime 1968", "Inglaterra", EN, Position.FWD, 102, 1968),
            P("Gerd Müller", "Prime 1972", "Alemania", DE, Position.FWD, 102, 1972),
            P("Xavi", "Prime 2010", "España", ES, Position.MID, 102, 2010),
            P("Iniesta", "Prime 2010", "España", ES, Position.MID, 102, 2010),
            P("Neuer", "Prime 2014", "Alemania", DE, Position.GK, 102, 2014),
            P("Cannavaro", "Prime 2006", "Italia", IT, Position.DEF, 102, 2006),
            P("Roberto Carlos", "Prime 2002", "Brasil", BR, Position.DEF, 102, 2002),
            P("Haaland", "Prime 2023", "Noruega", NO, Position.FWD, 102, 2023),
            P("Neymar", "Prime 2015", "Brasil", BR, Position.FWD, 102, 2015),
            P("Zico", "Prime 1982", "Brasil", BR, Position.MID, 101, 1982),
            P("Romário", "Prime 1994", "Brasil", BR, Position.FWD, 101, 1994),
            P("Modrić", "Prime 2018", "Croacia", HR, Position.MID, 101, 2018),
            P("Casillas", "Prime 2010", "España", ES, Position.GK, 101, 2010),

            // ---------- 100 ----------
            P("Rivaldo", "Prime 1999", "Brasil", BR, Position.MID, 100, 1999),
            P("Sócrates", "Prime 1982", "Brasil", BR, Position.MID, 100, 1982),
            P("Kaká", "Prime 2007", "Brasil", BR, Position.MID, 100, 2007),
            P("Figo", "Prime 2000", "Portugal", PT, Position.FWD, 100, 2000),
            P("Bergkamp", "Prime 1998", "P. Bajos", NL, Position.FWD, 100, 1998),
            P("Baresi", "Prime 1989", "Italia", IT, Position.DEF, 100, 1989),
        };
    }

    /// <summary>El prime cuya fuerza está más cerca del número sorteado.</summary>
    public static IEnumerable<Player> ClosestTo(int rating)
    {
        int mejor = All.Min(p => Math.Abs(p.Rating - rating));
        return All.Where(p => Math.Abs(p.Rating - rating) == mejor);
    }
}
