using OnceDeOro.Models;

namespace OnceDeOro.Data;

/// <summary>
/// Las versiones PRIME: el jugador en su mejor momento exacto. Son las únicas
/// cartas de 100 a 109 del juego y NO están en el mercado a propósito — si se
/// pudieran comprar, bastaría con juntar plata y el tramo de 100+ de las cajas
/// dejaría de valer nada. Sólo salen de una caja.
///
/// El Pelé de 96 del mercado y el "Pelé (Prime 1970)" de 108 son dos cartas
/// distintas: tener la primera no te da la segunda, pero sí te sirve para
/// mejorarla (ver GameService.Fuse).
///
/// Hay 11 por puesto para que se pueda armar un equipo entero de primes.
/// </summary>
public static class PrimeDatabase
{
    /// <summary>
    /// El nombre se arma como "NombreDeMercado (Prime AÑO)" y de ahí se recupera
    /// el del mercado para emparejarlos. Por eso el primer argumento tiene que ser
    /// EXACTAMENTE el nombre que usa PlayerDatabase, letra por letra.
    /// </summary>
    private const string Marca = " (Prime ";

    private static int _seq;
    private static Player P(string marketName, int year, string nation, string flag,
                            Position pos, int rating)
        => new()
        {
            Id = $"prime{++_seq}",
            Name = $"{marketName}{Marca}{year})",
            Nation = nation,
            Flag = flag,
            Pos = pos,
            Rating = rating,
            Era = year,
            IsLegend = true,
        };

    /// <summary>Devuelve el nombre de mercado de un prime ("Pelé (Prime 1970)" → "Pelé").</summary>
    public static string BaseName(string primeName)
    {
        int i = primeName.IndexOf(Marca, StringComparison.Ordinal);
        return i < 0 ? primeName : primeName[..i];
    }

    public static bool IsPrimeName(string name) => name.Contains(Marca, StringComparison.Ordinal);

    public static readonly IReadOnlyList<Player> All = Build();

    private static List<Player> Build()
    {
        _seq = 0;
        const string BR = "🇧🇷", AR = "🇦🇷", DE = "🇩🇪", NL = "🇳🇱", IT = "🇮🇹",
                     FR = "🇫🇷", ES = "🇪🇸", PT = "🇵🇹", HU = "🇭🇺", RU = "🇷🇺",
                     HR = "🇭🇷", NO = "🇳🇴", EN = "🏴󠁧󠁢󠁥󠁮󠁧󠁿", DK = "🇩🇰", CZ = "🇨🇿",
                     CL = "🇨🇱", CV = "🇨🇻";

        return new List<Player>
        {
            // ---------------- ARQUEROS (11) ----------------
            P("Lev Yashin", 1963, "Rusia", RU, Position.GK, 105),
            P("Gianluigi Buffon", 2006, "Italia", IT, Position.GK, 104),
            P("Manuel Neuer", 2014, "Alemania", DE, Position.GK, 103),
            P("Iker Casillas", 2010, "España", ES, Position.GK, 103),
            P("Dino Zoff", 1982, "Italia", IT, Position.GK, 102),
            P("Gordon Banks", 1970, "Inglaterra", EN, Position.GK, 102),
            P("Oliver Kahn", 2002, "Alemania", DE, Position.GK, 102),
            P("Peter Schmeichel", 1992, "Dinamarca", DK, Position.GK, 101),
            P("Petr Čech", 2005, "Chequia", CZ, Position.GK, 101),
            P("Vozinha", 2024, "Cabo Verde", CV, Position.GK, 101),
            P("Edwin van der Sar", 2008, "P. Bajos", NL, Position.GK, 100),

            // ---------------- DEFENSORES (11) ----------------
            P("Franz Beckenbauer", 1974, "Alemania", DE, Position.DEF, 106),
            P("Virgil van Dijk", 2019, "P. Bajos", NL, Position.DEF, 105),
            P("Franco Baresi", 1989, "Italia", IT, Position.DEF, 105),
            P("Paolo Maldini", 1994, "Italia", IT, Position.DEF, 104),
            P("Bobby Moore", 1966, "Inglaterra", EN, Position.DEF, 104),
            P("Carlos Alberto Torres", 1970, "Brasil", BR, Position.DEF, 103),
            P("Fabio Cannavaro", 2006, "Italia", IT, Position.DEF, 102),
            P("Alessandro Nesta", 2003, "Italia", IT, Position.DEF, 102),
            P("Cafú", 2002, "Brasil", BR, Position.DEF, 101),
            P("Roberto Carlos", 2002, "Brasil", BR, Position.DEF, 101),
            P("Elías Figueroa", 1974, "Chile", CL, Position.DEF, 100),

            // ---------------- MEDIOCAMPISTAS (11) ----------------
            P("Diego Maradona", 1986, "Argentina", AR, Position.MID, 108),
            P("Zinédine Zidane", 1998, "Francia", FR, Position.MID, 105),
            P("Ronaldinho", 2005, "Brasil", BR, Position.MID, 105),
            P("Michel Platini", 1984, "Francia", FR, Position.MID, 104),
            P("Lothar Matthäus", 1990, "Alemania", DE, Position.MID, 103),
            P("Andrés Iniesta", 2010, "España", ES, Position.MID, 103),
            P("Xavi Hernández", 2010, "España", ES, Position.MID, 102),
            P("Luka Modrić", 2018, "Croacia", HR, Position.MID, 102),
            P("Andrea Pirlo", 2006, "Italia", IT, Position.MID, 101),
            P("Bobby Charlton", 1966, "Inglaterra", EN, Position.MID, 101),
            P("Zico", 1982, "Brasil", BR, Position.MID, 100),

            // ---------------- DELANTEROS (11) ----------------
            P("Lionel Messi", 2012, "Argentina", AR, Position.FWD, 109),
            P("Pelé", 1970, "Brasil", BR, Position.FWD, 108),
            P("Cristiano Ronaldo", 2014, "Portugal", PT, Position.FWD, 107),
            P("Johan Cruyff", 1974, "P. Bajos", NL, Position.FWD, 106),
            P("Ronaldo", 1997, "Brasil", BR, Position.FWD, 106),
            P("Alfredo Di Stéfano", 1959, "Argentina", AR, Position.FWD, 105),
            P("Ferenc Puskás", 1954, "Hungría", HU, Position.FWD, 104),
            P("Kylian Mbappé", 2022, "Francia", FR, Position.FWD, 103),
            P("Garrincha", 1962, "Brasil", BR, Position.FWD, 103),
            P("Marco van Basten", 1988, "P. Bajos", NL, Position.FWD, 102),
            P("Erling Haaland", 2023, "Noruega", NO, Position.FWD, 101),
        };
    }

    /// <summary>Los primes cuya fuerza está más cerca del número sorteado.</summary>
    public static IEnumerable<Player> ClosestTo(int rating)
    {
        int mejor = All.Min(p => Math.Abs(p.Rating - rating));
        return All.Where(p => Math.Abs(p.Rating - rating) == mejor);
    }
}
