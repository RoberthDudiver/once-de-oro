namespace OnceDeOro.Data;

/// <summary>Palmarés REAL de una selección nacional.</summary>
public sealed class CountryFacts
{
    public required string Name { get; init; }
    public required string Confed { get; init; }
    /// <summary>Años en que ganó el Mundial.</summary>
    public int[] WorldCups { get; init; } = Array.Empty<int>();
    /// <summary>Veces subcampeón del Mundial.</summary>
    public int WorldCupRunnerUp { get; init; }
    /// <summary>Títulos continentales (Copa América / Eurocopa / Copa Africana / etc.).</summary>
    public int Continental { get; init; }
    public string ContinentalName { get; init; } = "";
    /// <summary>Dato distintivo, corto.</summary>
    public string Note { get; init; } = "";
}

/// <summary>
/// Datos REALES de la carrera de un jugador. Curados a mano porque no existe una
/// API libre que cubra desde 1930 hasta hoy (Wikidata devuelve datos erróneos o
/// vacíos para los históricos, y TheSportsDB directamente no trae estadísticas).
/// Sólo se cargan hechos bien documentados; si de un jugador no hay datos fiables,
/// simplemente no aparece esta sección.
/// </summary>
public sealed class PlayerFacts
{
    /// <summary>Partidos con su selección.</summary>
    public int Caps { get; init; }
    /// <summary>Goles con su selección.</summary>
    public int IntGoals { get; init; }
    /// <summary>Años de los Mundiales que ganó.</summary>
    public int[] WorldCups { get; init; } = Array.Empty<int>();
    /// <summary>Balones de Oro.</summary>
    public int BallonDor { get; init; }
    /// <summary>Copas de Europa / Champions League.</summary>
    public int EuropeanCups { get; init; }
    /// <summary>El dato que lo define.</summary>
    public string Note { get; init; } = "";
}

/// <summary>Palmarés real de selecciones y leyendas.</summary>
public static class RealData
{
    // ============================================================
    //  SELECCIONES — títulos oficiales
    // ============================================================
    public static readonly Dictionary<string, CountryFacts> Countries = new()
    {
        ["Brasil"] = new() { Name = "Brasil", Confed = "CONMEBOL", WorldCups = new[] { 1958, 1962, 1970, 1994, 2002 }, WorldCupRunnerUp = 2, Continental = 9, ContinentalName = "Copa América", Note = "La única que jugó todos los Mundiales" },
        ["Alemania"] = new() { Name = "Alemania", Confed = "UEFA", WorldCups = new[] { 1954, 1974, 1990, 2014 }, WorldCupRunnerUp = 4, Continental = 3, ContinentalName = "Eurocopa", Note = "8 finales de Mundial, récord junto a Brasil" },
        ["Italia"] = new() { Name = "Italia", Confed = "UEFA", WorldCups = new[] { 1934, 1938, 1982, 2006 }, WorldCupRunnerUp = 2, Continental = 2, ContinentalName = "Eurocopa", Note = "Campeona en 2006 y ausente en 2018 y 2022" },
        ["Argentina"] = new() { Name = "Argentina", Confed = "CONMEBOL", WorldCups = new[] { 1978, 1986, 2022 }, WorldCupRunnerUp = 3, Continental = 16, ContinentalName = "Copa América", Note = "Récord de Copas América junto a Uruguay" },
        ["Francia"] = new() { Name = "Francia", Confed = "UEFA", WorldCups = new[] { 1998, 2018 }, WorldCupRunnerUp = 2, Continental = 2, ContinentalName = "Eurocopa", Note = "Campeona en casa en 1998" },
        ["Uruguay"] = new() { Name = "Uruguay", Confed = "CONMEBOL", WorldCups = new[] { 1930, 1950 }, Continental = 15, ContinentalName = "Copa América", Note = "Ganó el primer Mundial y el Maracanazo" },
        ["Inglaterra"] = new() { Name = "Inglaterra", Confed = "UEFA", WorldCups = new[] { 1966 }, WorldCupRunnerUp = 0, Continental = 0, ContinentalName = "Eurocopa", Note = "Su único título fue en casa, en 1966" },
        ["España"] = new() { Name = "España", Confed = "UEFA", WorldCups = new[] { 2010 }, Continental = 4, ContinentalName = "Eurocopa", Note = "Euro-Mundial-Euro entre 2008 y 2012" },
        ["Países Bajos"] = new() { Name = "Países Bajos", Confed = "UEFA", WorldCupRunnerUp = 3, Continental = 1, ContinentalName = "Eurocopa", Note = "Tres finales de Mundial, ninguna ganada" },
        ["Portugal"] = new() { Name = "Portugal", Confed = "UEFA", Continental = 1, ContinentalName = "Eurocopa", Note = "Campeona de la Euro 2016" },
        ["Bélgica"] = new() { Name = "Bélgica", Confed = "UEFA", Note = "Tercera en el Mundial 2018, su mejor puesto" },
        ["Croacia"] = new() { Name = "Croacia", Confed = "UEFA", WorldCupRunnerUp = 1, Note = "Finalista en 2018 y tercera en 2022" },
        ["Colombia"] = new() { Name = "Colombia", Confed = "CONMEBOL", Continental = 1, ContinentalName = "Copa América", Note = "Campeona de América en 2001, sin perder ni recibir goles" },
        ["Chile"] = new() { Name = "Chile", Confed = "CONMEBOL", Continental = 2, ContinentalName = "Copa América", Note = "Bicampeona de América en 2015 y 2016" },
        ["Paraguay"] = new() { Name = "Paraguay", Confed = "CONMEBOL", Continental = 2, ContinentalName = "Copa América", Note = "Cuartos de final en el Mundial 2010" },
        ["Perú"] = new() { Name = "Perú", Confed = "CONMEBOL", Continental = 2, ContinentalName = "Copa América", Note = "Campeona de América en 1939 y 1975" },
        ["Venezuela"] = new() { Name = "Venezuela", Confed = "CONMEBOL", Note = "La única de Sudamérica que aún no jugó un Mundial" },
        ["Ecuador"] = new() { Name = "Ecuador", Confed = "CONMEBOL", Note = "Octavos de final en el Mundial 2006" },
        ["México"] = new() { Name = "México", Confed = "CONCACAF", Continental = 12, ContinentalName = "Copa Oro", Note = "Anfitriona de los Mundiales 1970, 1986 y 2026" },
        ["Estados Unidos"] = new() { Name = "Estados Unidos", Confed = "CONCACAF", Continental = 7, ContinentalName = "Copa Oro", Note = "Tercera en el primer Mundial, 1930" },
        ["Camerún"] = new() { Name = "Camerún", Confed = "CAF", Continental = 5, ContinentalName = "Copa Africana", Note = "Cuartos en 1990, hazaña africana" },
        ["Senegal"] = new() { Name = "Senegal", Confed = "CAF", Continental = 1, ContinentalName = "Copa Africana", Note = "Campeona de África en 2021" },
        ["Nigeria"] = new() { Name = "Nigeria", Confed = "CAF", Continental = 3, ContinentalName = "Copa Africana", Note = "Oro olímpico en Atlanta 1996" },
        ["Ghana"] = new() { Name = "Ghana", Confed = "CAF", Continental = 4, ContinentalName = "Copa Africana", Note = "Cuartos en 2010, a un penal de la semifinal" },
        ["Egipto"] = new() { Name = "Egipto", Confed = "CAF", Continental = 7, ContinentalName = "Copa Africana", Note = "Récord de títulos africanos" },
        ["Costa de Marfil"] = new() { Name = "Costa de Marfil", Confed = "CAF", Continental = 3, ContinentalName = "Copa Africana", Note = "Campeona en casa en 2023" },
        ["Marruecos"] = new() { Name = "Marruecos", Confed = "CAF", Continental = 1, ContinentalName = "Copa Africana", Note = "Primera africana en llegar a semifinales, 2022" },
        ["Corea del Sur"] = new() { Name = "Corea del Sur", Confed = "AFC", Continental = 2, ContinentalName = "Copa Asiática", Note = "Semifinalista en su Mundial, 2002" },
        ["Japón"] = new() { Name = "Japón", Confed = "AFC", Continental = 4, ContinentalName = "Copa Asiática", Note = "Récord de títulos asiáticos" },
        ["Rusia"] = new() { Name = "Rusia", Confed = "UEFA", Continental = 1, ContinentalName = "Eurocopa", Note = "Como URSS ganó la primera Eurocopa, 1960" },
        ["Ucrania"] = new() { Name = "Ucrania", Confed = "UEFA", Note = "Cuartos en su primer Mundial, 2006" },
        ["Bulgaria"] = new() { Name = "Bulgaria", Confed = "UEFA", Note = "Cuarta en el Mundial 1994" },
        ["Rumania"] = new() { Name = "Rumania", Confed = "UEFA", Note = "Cuartos en 1994 con la generación de Hagi" },
        ["Dinamarca"] = new() { Name = "Dinamarca", Confed = "UEFA", Continental = 1, ContinentalName = "Eurocopa", Note = "Campeona de la Euro 1992 sin haberse clasificado" },
        ["Suecia"] = new() { Name = "Suecia", Confed = "UEFA", WorldCupRunnerUp = 1, Note = "Finalista en casa en 1958, ante Brasil" },
        ["Noruega"] = new() { Name = "Noruega", Confed = "UEFA", Note = "Nunca perdió con Brasil en un Mundial" },
        ["Polonia"] = new() { Name = "Polonia", Confed = "UEFA", Note = "Tercera en 1974 y 1982" },
        ["Hungría"] = new() { Name = "Hungría", Confed = "UEFA", WorldCupRunnerUp = 2, Note = "El Equipo de Oro invicto entre 1950 y 1956" },
        ["Chequia"] = new() { Name = "Chequia", Confed = "UEFA", WorldCupRunnerUp = 2, Continental = 1, ContinentalName = "Eurocopa", Note = "Como Checoslovaquia, campeona de Europa en 1976" },
        ["Austria"] = new() { Name = "Austria", Confed = "UEFA", Note = "El Wunderteam de los años 30" },
        ["Suiza"] = new() { Name = "Suiza", Confed = "UEFA", Note = "Anfitriona de la sede de la FIFA" },
        ["Serbia"] = new() { Name = "Serbia", Confed = "UEFA", Note = "Como Yugoslavia, cuarta en 1930 y 1962" },
        ["Escocia"] = new() { Name = "Escocia", Confed = "UEFA", Note = "Jugó el primer partido internacional de la historia, 1872" },
        ["Irlanda del Norte"] = new() { Name = "Irlanda del Norte", Confed = "UEFA", Note = "Cuartos en 1958 con George Best por llegar" },
        ["Irlanda"] = new() { Name = "Irlanda", Confed = "UEFA", Note = "Cuartos en su primer Mundial, 1990" },
        ["Georgia"] = new() { Name = "Georgia", Confed = "UEFA", Note = "Debutó en una Eurocopa en 2024" },
        ["Turquía"] = new() { Name = "Turquía", Confed = "UEFA", Note = "Tercera en el Mundial 2002" },
        ["Eslovenia"] = new() { Name = "Eslovenia", Confed = "UEFA", Note = "Dos Mundiales pese a ser de los países más chicos" },
        ["Costa Rica"] = new() { Name = "Costa Rica", Confed = "CONCACAF", Continental = 3, ContinentalName = "Copa Centroamericana", Note = "Cuartos en 2014 sin perder un partido" },
        ["Canadá"] = new() { Name = "Canadá", Confed = "CONCACAF", Continental = 1, ContinentalName = "Copa Oro", Note = "Coanfitriona del Mundial 2026" },
        ["Liberia"] = new() { Name = "Liberia", Confed = "CAF", Note = "Cuna de George Weah, hoy su presidente" },
        ["Bolivia"] = new() { Name = "Bolivia", Confed = "CONMEBOL", Continental = 1, ContinentalName = "Copa América", Note = "Campeona de América en casa, 1963" },
    };

    // ============================================================
    //  LEYENDAS — datos de carrera bien documentados
    // ============================================================
    public static readonly Dictionary<string, PlayerFacts> Players = new()
    {
        ["Pelé"] = new() { Caps = 92, IntGoals = 77, WorldCups = new[] { 1958, 1962, 1970 }, Note = "El único futbolista con tres Mundiales ganados" },
        ["Diego Maradona"] = new() { Caps = 91, IntGoals = 34, WorldCups = new[] { 1986 }, Note = "Llevó a Argentina al título casi en soledad en 1986" },
        ["Lionel Messi"] = new() { Caps = 194, IntGoals = 112, WorldCups = new[] { 2022 }, BallonDor = 8, EuropeanCups = 4, Note = "Récord absoluto de Balones de Oro" },
        ["Cristiano Ronaldo"] = new() { Caps = 217, IntGoals = 138, BallonDor = 5, EuropeanCups = 5, Note = "Máximo goleador histórico de selecciones" },
        ["Johan Cruyff"] = new() { Caps = 48, IntGoals = 33, BallonDor = 3, EuropeanCups = 3, Note = "Padre del fútbol total; perdió la final de 1974" },
        ["Franz Beckenbauer"] = new() { Caps = 103, IntGoals = 14, WorldCups = new[] { 1974 }, BallonDor = 2, EuropeanCups = 3, Note = "Campeón del Mundo como jugador y como técnico" },
        ["Alfredo Di Stéfano"] = new() { BallonDor = 2, EuropeanCups = 5, Note = "Cinco Copas de Europa seguidas; nunca jugó un Mundial" },
        ["Ferenc Puskás"] = new() { Caps = 85, IntGoals = 84, EuropeanCups = 3, Note = "84 goles en 85 partidos con Hungría" },
        ["Zinédine Zidane"] = new() { Caps = 108, IntGoals = 31, WorldCups = new[] { 1998 }, BallonDor = 1, EuropeanCups = 1, Note = "Dos goles en la final del Mundial 1998" },
        ["Ronaldo"] = new() { Caps = 98, IntGoals = 62, WorldCups = new[] { 1994, 2002 }, BallonDor = 2, Note = "15 goles en Mundiales; 8 de ellos en 2002" },
        ["Ronaldinho"] = new() { Caps = 97, IntGoals = 33, WorldCups = new[] { 2002 }, BallonDor = 1, EuropeanCups = 1, Note = "Ovacionado por el público del Real Madrid en 2005" },
        ["Lev Yashin"] = new() { Caps = 74, BallonDor = 1, Note = "El único arquero que ganó el Balón de Oro" },
        ["Paolo Maldini"] = new() { Caps = 126, IntGoals = 7, EuropeanCups = 5, Note = "25 temporadas en el Milan; nunca ganó un Mundial" },
        ["Franco Baresi"] = new() { Caps = 81, IntGoals = 1, WorldCups = new[] { 1982 }, EuropeanCups = 3, Note = "Jugó la final de 1994 lesionado" },
        ["Bobby Moore"] = new() { Caps = 108, IntGoals = 2, WorldCups = new[] { 1966 }, Note = "Capitán del único título de Inglaterra" },
        ["Gerd Müller"] = new() { Caps = 62, IntGoals = 68, WorldCups = new[] { 1974 }, BallonDor = 1, EuropeanCups = 3, Note = "68 goles en 62 partidos; marcó en la final de 1974" },
        ["Michel Platini"] = new() { Caps = 72, IntGoals = 41, BallonDor = 3, EuropeanCups = 1, Note = "9 goles en la Eurocopa 1984, récord del torneo" },
        ["Marco van Basten"] = new() { Caps = 58, IntGoals = 24, BallonDor = 3, EuropeanCups = 2, Note = "Volea imposible en la final de la Euro 1988" },
        ["Lothar Matthäus"] = new() { Caps = 150, IntGoals = 23, WorldCups = new[] { 1990 }, BallonDor = 1, Note = "Récord alemán de partidos y cinco Mundiales jugados" },
        ["Garrincha"] = new() { Caps = 50, IntGoals = 12, WorldCups = new[] { 1958, 1962 }, Note = "Brasil nunca perdió un partido con él y Pelé juntos" },
        ["Eusébio"] = new() { Caps = 64, IntGoals = 41, BallonDor = 1, EuropeanCups = 1, Note = "Goleador del Mundial 1966 con 9 goles" },
        ["George Best"] = new() { Caps = 37, IntGoals = 9, BallonDor = 1, EuropeanCups = 1, Note = "Genio que nunca pudo jugar un Mundial" },
        ["Gianluigi Buffon"] = new() { Caps = 176, WorldCups = new[] { 2006 }, Note = "Campeón del Mundo sin recibir goles en juego hasta la final" },
        ["Iker Casillas"] = new() { Caps = 167, WorldCups = new[] { 2010 }, EuropeanCups = 3, Note = "Capitán del Mundial 2010 y de dos Eurocopas" },
        ["Fabio Cannavaro"] = new() { Caps = 136, WorldCups = new[] { 2006 }, BallonDor = 1, Note = "El último defensor en ganar el Balón de Oro" },
        ["Andrés Iniesta"] = new() { Caps = 131, IntGoals = 13, WorldCups = new[] { 2010 }, EuropeanCups = 4, Note = "Autor del gol que le dio a España su único Mundial" },
        ["Xavi Hernández"] = new() { Caps = 133, IntGoals = 13, WorldCups = new[] { 2010 }, EuropeanCups = 4, Note = "Cerebro del España y Barcelona que ganaron todo" },
        ["Roberto Baggio"] = new() { Caps = 56, IntGoals = 27, BallonDor = 1, Note = "Erró el penal decisivo de la final de 1994" },
        ["Zico"] = new() { Caps = 71, IntGoals = 48, Note = "Figura del Brasil de 1982, el mejor que no fue campeón" },
        ["Sócrates"] = new() { Caps = 60, IntGoals = 22, Note = "Médico y capitán del Brasil de 1982" },
        ["Kylian Mbappé"] = new() { Caps = 86, IntGoals = 48, WorldCups = new[] { 2018 }, Note = "Triplete en la final del Mundial 2022 y aun así perdió" },
        ["Ruud Gullit"] = new() { Caps = 66, IntGoals = 17, BallonDor = 1, EuropeanCups = 2, Note = "Capitán de la Holanda campeona de Europa en 1988" },
        ["Bobby Charlton"] = new() { Caps = 106, IntGoals = 49, WorldCups = new[] { 1966 }, BallonDor = 1, EuropeanCups = 1, Note = "Sobrevivió al accidente de Múnich y fue campeón del mundo" },
        ["Dino Zoff"] = new() { Caps = 112, WorldCups = new[] { 1982 }, Note = "Campeón del mundo a los 40 años, el más veterano" },
        ["Gordon Banks"] = new() { Caps = 73, WorldCups = new[] { 1966 }, Note = "La atajada del siglo a Pelé en 1970" },
        ["Andrea Pirlo"] = new() { Caps = 116, IntGoals = 13, WorldCups = new[] { 2006 }, EuropeanCups = 2, Note = "El penal picado a Inglaterra en la Euro 2012" },
        ["Luka Modrić"] = new() { Caps = 180, IntGoals = 27, BallonDor = 1, EuropeanCups = 6, Note = "Cortó la hegemonía de Messi y Cristiano en 2018" },
        ["Carlos Alberto Torres"] = new() { Caps = 53, WorldCups = new[] { 1970 }, Note = "Autor del gol más famoso de la historia de los Mundiales" },
        ["Roberto Carlos"] = new() { Caps = 125, IntGoals = 11, WorldCups = new[] { 2002 }, EuropeanCups = 3, Note = "El tiro libre imposible a Francia en 1997" },
        ["Cafú"] = new() { Caps = 142, WorldCups = new[] { 1994, 2002 }, Note = "El único que jugó tres finales de Mundial seguidas" },
        ["Romário"] = new() { Caps = 70, IntGoals = 55, WorldCups = new[] { 1994 }, BallonDor = 1, Note = "Figura del Mundial 1994" },
        ["Rivaldo"] = new() { Caps = 74, IntGoals = 35, WorldCups = new[] { 2002 }, BallonDor = 1, Note = "Parte del trío mágico con Ronaldo y Ronaldinho" },
        ["Kaká"] = new() { Caps = 92, IntGoals = 29, WorldCups = new[] { 2002 }, BallonDor = 1, EuropeanCups = 1, Note = "El último en ganar el Balón de Oro antes de Messi y Cristiano" },
        ["Thierry Henry"] = new() { Caps = 123, IntGoals = 51, WorldCups = new[] { 1998 }, EuropeanCups = 1, Note = "Máximo goleador histórico de Francia hasta Giroud" },
        ["Juan Arango"] = new() { Caps = 129, IntGoals = 23, Note = "Máximo goleador histórico de la Vinotinto" },
        ["Diego Forlán"] = new() { Caps = 112, IntGoals = 36, Note = "Mejor jugador del Mundial 2010" },
        ["Manuel Neuer"] = new() { Caps = 124, WorldCups = new[] { 2014 }, EuropeanCups = 2, Note = "Redefinió el puesto con el arquero-líbero" },
        ["Erling Haaland"] = new() { Caps = 42, IntGoals = 39, EuropeanCups = 1, Note = "El goleador más precoz de la Champions" },
        ["Kevin De Bruyne"] = new() { Caps = 109, IntGoals = 30, EuropeanCups = 1, Note = "Récord de asistencias de la generación dorada belga" },
        ["Robert Lewandowski"] = new() { Caps = 158, IntGoals = 85, EuropeanCups = 1, Note = "Cinco goles en nueve minutos en la Bundesliga" },
        ["Luis Suárez"] = new() { Caps = 143, IntGoals = 69, Note = "Máximo goleador histórico de Uruguay" },
        ["Andriy Shevchenko"] = new() { Caps = 111, IntGoals = 48, BallonDor = 1, EuropeanCups = 1, Note = "Llevó a Ucrania a cuartos en su primer Mundial" },
        ["George Weah"] = new() { BallonDor = 1, Note = "Único africano con Balón de Oro; hoy presidente de Liberia" },
        ["Hristo Stoichkov"] = new() { Caps = 83, IntGoals = 37, BallonDor = 1, EuropeanCups = 1, Note = "Llevó a Bulgaria a semifinales en 1994" },
        ["Samuel Eto'o"] = new() { Caps = 118, IntGoals = 56, EuropeanCups = 3, Note = "Máximo goleador histórico de la Copa Africana" },
        ["Didier Drogba"] = new() { Caps = 105, IntGoals = 65, EuropeanCups = 1, Note = "Su llamado ayudó a frenar la guerra civil marfileña" },
        ["Mohamed Salah"] = new() { Caps = 105, IntGoals = 60, EuropeanCups = 1, Note = "Máximo goleador histórico de Egipto" },
        ["Zlatan Ibrahimović"] = new() { Caps = 122, IntGoals = 62, Note = "Campeón en cuatro países distintos" },
        ["Sergio Ramos"] = new() { Caps = 180, IntGoals = 23, WorldCups = new[] { 2010 }, EuropeanCups = 4, Note = "El cabezazo del minuto 93 en la final de 2014" },
        ["Peter Schmeichel"] = new() { Caps = 129, EuropeanCups = 1, Note = "Campeón de Europa con Dinamarca en 1992" },
        ["Ronald Koeman"] = new() { Caps = 78, IntGoals = 14, EuropeanCups = 1, Note = "Su tiro libre le dio al Barcelona su primera Copa de Europa" },
    };

    /// <summary>
    /// El roster abrevia algunos países ("P. Bajos", "EE. UU."). Sin estos alias
    /// esos jugadores se quedaban sin los datos de su selección.
    /// </summary>
    private static readonly Dictionary<string, string> Alias = new()
    {
        ["P. Bajos"] = "Países Bajos",
        ["Holanda"] = "Países Bajos",
        ["EE. UU."] = "Estados Unidos",
        ["C. de Marfil"] = "Costa de Marfil",
        ["Corea"] = "Corea del Sur",
        ["Canada"] = "Canadá",
        ["Checoslovaquia"] = "Chequia",
        ["URSS"] = "Rusia",
    };

    public static CountryFacts? Country(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (Alias.TryGetValue(name, out var real)) name = real;
        return Countries.TryGetValue(name, out var c) ? c : null;
    }

    public static PlayerFacts? Player(string name) =>
        Players.TryGetValue(name, out var p) ? p : null;
}
