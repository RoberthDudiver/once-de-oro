using OnceDeOro.Models;

namespace OnceDeOro.Data;

/// <summary>
/// Roster de leyendas de los Mundiales (1950-2026) + agentes libres para armar de cero.
/// Los nombres son datos históricos; las fuerzas son un juicio editorial para el juego.
/// </summary>
public static class PlayerDatabase
{
    private static int _seq;
    private static Player P(string name, string nation, string flag, Position pos, int rating, int era, bool legend = false)
        => new()
        {
            Id = $"p{++_seq}",
            Name = name,
            Nation = nation,
            Flag = flag,
            Pos = pos,
            Rating = rating,
            Era = era,
            IsLegend = legend
        };

    public static readonly IReadOnlyList<Player> All = Build();

    private static List<Player> Build()
    {
        _seq = 0;
        const string BR = "🇧🇷", AR = "🇦🇷", DE = "🇩🇪", NL = "🇳🇱", IT = "🇮🇹",
                     FR = "🇫🇷", EN = "🏴󠁧󠁢󠁥󠁮󠁧󠁿", ES = "🇪🇸", PT = "🇵🇹", UY = "🇺🇾",
                     HR = "🇭🇷", BE = "🇧🇪", PL = "🇵🇱", SE = "🇸🇪", DK = "🇩🇰",
                     NO = "🇳🇴", CO = "🇨🇴", CL = "🇨🇱", MX = "🇲🇽", CM = "🇨🇲",
                     CI = "🇨🇮", EG = "🇪🇬", SN = "🇸🇳", GH = "🇬🇭", NG = "🇳🇬",
                     KR = "🇰🇷", JP = "🇯🇵", RU = "🇷🇺", UA = "🇺🇦", BG = "🇧🇬",
                     RO = "🇷🇴", US = "🇺🇸";

        var l = new List<Player>
        {
            // ---------- BRASIL ----------
            P("Pelé", "Brasil", BR, Position.FWD, 96, 1970, true),
            P("Garrincha", "Brasil", BR, Position.FWD, 92, 1962, true),
            P("Ronaldo", "Brasil", BR, Position.FWD, 94, 2002, true),
            P("Romário", "Brasil", BR, Position.FWD, 91, 1994, true),
            P("Ronaldinho", "Brasil", BR, Position.MID, 92, 2006, true),
            P("Rivaldo", "Brasil", BR, Position.MID, 90, 2002, true),
            P("Zico", "Brasil", BR, Position.MID, 90, 1982, true),
            P("Sócrates", "Brasil", BR, Position.MID, 90, 1982, true),
            P("Kaká", "Brasil", BR, Position.MID, 89, 2006, true),
            P("Neymar", "Brasil", BR, Position.FWD, 90, 2018, true),
            P("Roberto Carlos", "Brasil", BR, Position.DEF, 90, 2002, true),
            P("Cafú", "Brasil", BR, Position.DEF, 90, 2002, true),
            P("Carlos Alberto", "Brasil", BR, Position.DEF, 87, 1970, true),
            P("Nílton Santos", "Brasil", BR, Position.DEF, 85, 1958),
            P("Dunga", "Brasil", BR, Position.MID, 82, 1994),
            P("Bebeto", "Brasil", BR, Position.FWD, 84, 1994),
            P("Vinícius Jr.", "Brasil", BR, Position.FWD, 88, 2026, true),
            P("Alisson", "Brasil", BR, Position.GK, 88, 2022),
            P("Taffarel", "Brasil", BR, Position.GK, 84, 1994),
            P("Thiago Silva", "Brasil", BR, Position.DEF, 86, 2014),

            // ---------- ARGENTINA ----------
            P("Diego Maradona", "Argentina", AR, Position.MID, 96, 1986, true),
            P("Lionel Messi", "Argentina", AR, Position.FWD, 96, 2022, true),
            P("Alfredo Di Stéfano", "Argentina", AR, Position.FWD, 93, 1958, true),
            P("Mario Kempes", "Argentina", AR, Position.FWD, 88, 1978, true),
            P("Gabriel Batistuta", "Argentina", AR, Position.FWD, 90, 1998, true),
            P("Daniel Passarella", "Argentina", AR, Position.DEF, 90, 1978),
            P("Juan Román Riquelme", "Argentina", AR, Position.MID, 88, 2006, true),
            P("Ángel Di María", "Argentina", AR, Position.MID, 86, 2022, true),
            P("Javier Zanetti", "Argentina", AR, Position.DEF, 85, 1998),
            P("Fernando Redondo", "Argentina", AR, Position.MID, 85, 1998),
            P("Sergio Agüero", "Argentina", AR, Position.FWD, 87, 2014),
            P("Javier Mascherano", "Argentina", AR, Position.MID, 84, 2014),
            P("Emiliano Martínez", "Argentina", AR, Position.GK, 87, 2022, true),
            P("Nicolás Otamendi", "Argentina", AR, Position.DEF, 82, 2022),
            P("Ubaldo Fillol", "Argentina", AR, Position.GK, 89, 1978),
            P("Julián Álvarez", "Argentina", AR, Position.FWD, 85, 2026),

            // ---------- ALEMANIA ----------
            P("Franz Beckenbauer", "Alemania", DE, Position.DEF, 94, 1974, true),
            P("Gerd Müller", "Alemania", DE, Position.FWD, 92, 1974, true),
            P("Lothar Matthäus", "Alemania", DE, Position.MID, 92, 1990, true),
            P("Miroslav Klose", "Alemania", DE, Position.FWD, 87, 2014, true),
            P("Oliver Kahn", "Alemania", DE, Position.GK, 90, 2002, true),
            P("Manuel Neuer", "Alemania", DE, Position.GK, 91, 2014, true),
            P("Jürgen Klinsmann", "Alemania", DE, Position.FWD, 86, 1990),
            P("Karl-Heinz Rummenigge", "Alemania", DE, Position.FWD, 87, 1982),
            P("Paul Breitner", "Alemania", DE, Position.DEF, 85, 1974),
            P("Matthias Sammer", "Alemania", DE, Position.DEF, 85, 1996),
            P("Philipp Lahm", "Alemania", DE, Position.DEF, 87, 2014, true),
            P("Bastian Schweinsteiger", "Alemania", DE, Position.MID, 87, 2014, true),
            P("Toni Kroos", "Alemania", DE, Position.MID, 89, 2014, true),
            P("Thomas Müller", "Alemania", DE, Position.FWD, 86, 2014),

            // ---------- PAÍSES BAJOS ----------
            P("Johan Cruyff", "P. Bajos", NL, Position.FWD, 95, 1974, true),
            P("Marco van Basten", "P. Bajos", NL, Position.FWD, 91, 1988, true),
            P("Ruud Gullit", "P. Bajos", NL, Position.MID, 90, 1988, true),
            P("Frank Rijkaard", "P. Bajos", NL, Position.MID, 88, 1988, true),
            P("Dennis Bergkamp", "P. Bajos", NL, Position.FWD, 89, 1998, true),
            P("Johan Neeskens", "P. Bajos", NL, Position.MID, 86, 1974),
            P("Arjen Robben", "P. Bajos", NL, Position.FWD, 88, 2010, true),
            P("Wesley Sneijder", "P. Bajos", NL, Position.MID, 87, 2010),
            P("Virgil van Dijk", "P. Bajos", NL, Position.DEF, 89, 2022, true),
            P("Ruud van Nistelrooy", "P. Bajos", NL, Position.FWD, 86, 2006),
            P("Edwin van der Sar", "P. Bajos", NL, Position.GK, 89, 2010),
            P("Ronald Koeman", "P. Bajos", NL, Position.DEF, 84, 1994),

            // ---------- ITALIA ----------
            P("Roberto Baggio", "Italia", IT, Position.FWD, 91, 1994, true),
            P("Paolo Maldini", "Italia", IT, Position.DEF, 92, 1994, true),
            P("Franco Baresi", "Italia", IT, Position.DEF, 93, 1994, true),
            P("Dino Zoff", "Italia", IT, Position.GK, 91, 1982, true),
            P("Paolo Rossi", "Italia", IT, Position.FWD, 87, 1982, true),
            P("Alessandro Del Piero", "Italia", IT, Position.FWD, 88, 2006, true),
            P("Francesco Totti", "Italia", IT, Position.MID, 88, 2006, true),
            P("Fabio Cannavaro", "Italia", IT, Position.DEF, 90, 2006, true),
            P("Gianluigi Buffon", "Italia", IT, Position.GK, 92, 2006, true),
            P("Andrea Pirlo", "Italia", IT, Position.MID, 90, 2012, true),
            P("Alessandro Nesta", "Italia", IT, Position.DEF, 90, 2006),
            P("Giacinto Facchetti", "Italia", IT, Position.DEF, 90, 1970),

            // ---------- FRANCIA ----------
            P("Zinédine Zidane", "Francia", FR, Position.MID, 95, 1998, true),
            P("Michel Platini", "Francia", FR, Position.MID, 92, 1984, true),
            P("Thierry Henry", "Francia", FR, Position.FWD, 90, 2006, true),
            P("Kylian Mbappé", "Francia", FR, Position.FWD, 93, 2022, true),
            P("Antoine Griezmann", "Francia", FR, Position.FWD, 88, 2018, true),
            P("N'Golo Kanté", "Francia", FR, Position.MID, 87, 2018, true),
            P("Just Fontaine", "Francia", FR, Position.FWD, 87, 1958, true),
            P("Raymond Kopa", "Francia", FR, Position.MID, 86, 1958),
            P("Patrick Vieira", "Francia", FR, Position.MID, 86, 1998),
            P("Marcel Desailly", "Francia", FR, Position.DEF, 85, 1998),
            P("Lilian Thuram", "Francia", FR, Position.DEF, 85, 1998),
            P("Hugo Lloris", "Francia", FR, Position.GK, 85, 2018),
            P("Karim Benzema", "Francia", FR, Position.FWD, 88, 2022),

            // ---------- INGLATERRA ----------
            P("Bobby Charlton", "Inglaterra", EN, Position.MID, 90, 1966, true),
            P("Bobby Moore", "Inglaterra", EN, Position.DEF, 92, 1966, true),
            P("Gordon Banks", "Inglaterra", EN, Position.GK, 91, 1966),
            P("Gary Lineker", "Inglaterra", EN, Position.FWD, 86, 1986),
            P("Paul Gascoigne", "Inglaterra", EN, Position.MID, 86, 1990),
            P("David Beckham", "Inglaterra", EN, Position.MID, 86, 2002, true),
            P("Steven Gerrard", "Inglaterra", EN, Position.MID, 87, 2006, true),
            P("Wayne Rooney", "Inglaterra", EN, Position.FWD, 86, 2006),
            P("Harry Kane", "Inglaterra", EN, Position.FWD, 88, 2022, true),
            P("Jude Bellingham", "Inglaterra", EN, Position.MID, 88, 2026, true),
            P("Rio Ferdinand", "Inglaterra", EN, Position.DEF, 85, 2006),

            // ---------- ESPAÑA ----------
            P("Xavi Hernández", "España", ES, Position.MID, 90, 2010, true),
            P("Andrés Iniesta", "España", ES, Position.MID, 91, 2010, true),
            P("Iker Casillas", "España", ES, Position.GK, 91, 2010, true),
            P("Carles Puyol", "España", ES, Position.DEF, 87, 2010, true),
            P("Sergio Ramos", "España", ES, Position.DEF, 88, 2010, true),
            P("David Villa", "España", ES, Position.FWD, 87, 2010),
            P("Raúl González", "España", ES, Position.FWD, 87, 2002),
            P("Sergio Busquets", "España", ES, Position.MID, 86, 2010),
            P("Pedri", "España", ES, Position.MID, 86, 2026),
            P("Rodri", "España", ES, Position.MID, 89, 2026, true),
            P("Lamine Yamal", "España", ES, Position.FWD, 87, 2026, true),

            // ---------- PORTUGAL ----------
            P("Eusébio", "Portugal", PT, Position.FWD, 92, 1966, true),
            P("Cristiano Ronaldo", "Portugal", PT, Position.FWD, 94, 2018, true),
            P("Luís Figo", "Portugal", PT, Position.MID, 89, 2006, true),
            P("Rui Costa", "Portugal", PT, Position.MID, 86, 2002),
            P("Bruno Fernandes", "Portugal", PT, Position.MID, 86, 2022),
            P("Bernardo Silva", "Portugal", PT, Position.MID, 86, 2022),
            P("Pepe", "Portugal", PT, Position.DEF, 84, 2018),

            // ---------- URUGUAY ----------
            P("Luis Suárez", "Uruguay", UY, Position.FWD, 89, 2010, true),
            P("Edinson Cavani", "Uruguay", UY, Position.FWD, 86, 2018),
            P("Diego Forlán", "Uruguay", UY, Position.FWD, 87, 2010, true),
            P("Enzo Francescoli", "Uruguay", UY, Position.MID, 87, 1990, true),
            P("Juan A. Schiaffino", "Uruguay", UY, Position.FWD, 86, 1950, true),
            P("Diego Godín", "Uruguay", UY, Position.DEF, 85, 2018),
            P("Federico Valverde", "Uruguay", UY, Position.MID, 87, 2026, true),

            // ---------- OTRAS ESTRELLAS ----------
            P("Luka Modrić", "Croacia", HR, Position.MID, 90, 2018, true),
            P("Davor Šuker", "Croacia", HR, Position.FWD, 85, 1998),
            P("Kevin De Bruyne", "Bélgica", BE, Position.MID, 90, 2018, true),
            P("Eden Hazard", "Bélgica", BE, Position.FWD, 87, 2018),
            P("Thibaut Courtois", "Bélgica", BE, Position.GK, 89, 2018, true),
            P("Robert Lewandowski", "Polonia", PL, Position.FWD, 89, 2022, true),
            P("Zbigniew Boniek", "Polonia", PL, Position.FWD, 84, 1982),
            P("Zlatan Ibrahimović", "Suecia", SE, Position.FWD, 88, 2006, true),
            P("Henrik Larsson", "Suecia", SE, Position.FWD, 84, 2002),
            P("Michael Laudrup", "Dinamarca", DK, Position.MID, 87, 1998, true),
            P("Peter Schmeichel", "Dinamarca", DK, Position.GK, 90, 1998),
            P("Erling Haaland", "Noruega", NO, Position.FWD, 91, 2026, true),
            P("Carlos Valderrama", "Colombia", CO, Position.MID, 85, 1994, true),
            P("James Rodríguez", "Colombia", CO, Position.MID, 85, 2014),
            P("René Higuita", "Colombia", CO, Position.GK, 82, 1990),
            P("Alexis Sánchez", "Chile", CL, Position.FWD, 84, 2014),
            P("Iván Zamorano", "Chile", CL, Position.FWD, 83, 1998),
            P("Hugo Sánchez", "México", MX, Position.FWD, 86, 1986, true),
            P("Rafael Márquez", "México", MX, Position.DEF, 83, 2006),
            P("Guillermo Ochoa", "México", MX, Position.GK, 82, 2014),
            P("Roger Milla", "Camerún", CM, Position.FWD, 84, 1990, true),
            P("Samuel Eto'o", "Camerún", CM, Position.FWD, 87, 2010, true),
            P("Didier Drogba", "C. de Marfil", CI, Position.FWD, 87, 2010, true),
            P("Mohamed Salah", "Egipto", EG, Position.FWD, 89, 2018, true),
            P("Sadio Mané", "Senegal", SN, Position.FWD, 86, 2022, true),
            P("Michael Essien", "Ghana", GH, Position.MID, 84, 2010),
            P("Jay-Jay Okocha", "Nigeria", NG, Position.MID, 85, 1998, true),
            P("Nwankwo Kanu", "Nigeria", NG, Position.FWD, 82, 1998),
            P("Son Heung-min", "Corea", KR, Position.FWD, 86, 2022, true),
            P("Hidetoshi Nakata", "Japón", JP, Position.MID, 83, 2002),
            P("Lev Yashin", "Rusia", RU, Position.GK, 93, 1966, true),
            P("Andriy Shevchenko", "Ucrania", UA, Position.FWD, 89, 2006, true),
            P("Hristo Stoichkov", "Bulgaria", BG, Position.FWD, 87, 1994, true),
            P("Gheorghe Hagi", "Rumania", RO, Position.MID, 87, 1994, true),
            P("Christian Pulisic", "EE. UU.", US, Position.FWD, 82, 2026),
        };

        const string VE = "🇻🇪";

        // ============================================================
        //  JUGADORES RECIENTES (últimos ~10 años · 2016-2026)
        // ============================================================
        l.AddRange(new[]
        {
            // ---- BRASIL ----
            P("Ederson", "Brasil", BR, Position.GK, 86, 2022),
            P("Marquinhos", "Brasil", BR, Position.DEF, 87, 2022),
            P("Éder Militão", "Brasil", BR, Position.DEF, 85, 2022),
            P("Danilo", "Brasil", BR, Position.DEF, 82, 2022),
            P("Casemiro", "Brasil", BR, Position.MID, 86, 2018),
            P("Bruno Guimarães", "Brasil", BR, Position.MID, 85, 2026),
            P("Lucas Paquetá", "Brasil", BR, Position.MID, 83, 2022),
            P("Raphinha", "Brasil", BR, Position.FWD, 86, 2026),
            P("Rodrygo", "Brasil", BR, Position.FWD, 85, 2026),
            P("Gabriel Jesus", "Brasil", BR, Position.FWD, 83, 2022),
            P("Richarlison", "Brasil", BR, Position.FWD, 82, 2022),
            P("Endrick", "Brasil", BR, Position.FWD, 81, 2026),

            // ---- ARGENTINA ----
            P("Cristian Romero", "Argentina", AR, Position.DEF, 86, 2022, true),
            P("Lisandro Martínez", "Argentina", AR, Position.DEF, 84, 2022),
            P("Nahuel Molina", "Argentina", AR, Position.DEF, 82, 2022),
            P("Enzo Fernández", "Argentina", AR, Position.MID, 85, 2026),
            P("Alexis Mac Allister", "Argentina", AR, Position.MID, 86, 2026, true),
            P("Rodrigo De Paul", "Argentina", AR, Position.MID, 84, 2022),
            P("Giovani Lo Celso", "Argentina", AR, Position.MID, 81, 2022),
            P("Lautaro Martínez", "Argentina", AR, Position.FWD, 88, 2026, true),
            P("Paulo Dybala", "Argentina", AR, Position.FWD, 85, 2022),
            P("Nico González", "Argentina", AR, Position.FWD, 82, 2026),

            // ---- FRANCIA ----
            P("Mike Maignan", "Francia", FR, Position.GK, 87, 2026),
            P("Jules Koundé", "Francia", FR, Position.DEF, 85, 2026),
            P("William Saliba", "Francia", FR, Position.DEF, 86, 2026, true),
            P("Dayot Upamecano", "Francia", FR, Position.DEF, 84, 2026),
            P("Theo Hernández", "Francia", FR, Position.DEF, 85, 2026),
            P("Aurélien Tchouaméni", "Francia", FR, Position.MID, 86, 2026, true),
            P("Eduardo Camavinga", "Francia", FR, Position.MID, 84, 2026),
            P("Adrien Rabiot", "Francia", FR, Position.MID, 83, 2022),
            P("Ousmane Dembélé", "Francia", FR, Position.FWD, 87, 2026, true),
            P("Marcus Thuram", "Francia", FR, Position.FWD, 84, 2026),
            P("Kingsley Coman", "Francia", FR, Position.FWD, 84, 2022),

            // ---- ALEMANIA ----
            P("Marc-André ter Stegen", "Alemania", DE, Position.GK, 88, 2026),
            P("Antonio Rüdiger", "Alemania", DE, Position.DEF, 87, 2026, true),
            P("Joshua Kimmich", "Alemania", DE, Position.MID, 88, 2026, true),
            P("Ilkay Gündoğan", "Alemania", DE, Position.MID, 85, 2022),
            P("Jamal Musiala", "Alemania", DE, Position.MID, 87, 2026, true),
            P("Florian Wirtz", "Alemania", DE, Position.MID, 87, 2026, true),
            P("Kai Havertz", "Alemania", DE, Position.FWD, 84, 2026),
            P("Leroy Sané", "Alemania", DE, Position.FWD, 84, 2022),
            P("Serge Gnabry", "Alemania", DE, Position.FWD, 83, 2022),

            // ---- ESPAÑA ----
            P("Unai Simón", "España", ES, Position.GK, 85, 2026),
            P("Dani Carvajal", "España", ES, Position.DEF, 85, 2026),
            P("Aymeric Laporte", "España", ES, Position.DEF, 84, 2022),
            P("Marc Cucurella", "España", ES, Position.DEF, 83, 2026),
            P("Gavi", "España", ES, Position.MID, 84, 2026),
            P("Fabián Ruiz", "España", ES, Position.MID, 84, 2026),
            P("Dani Olmo", "España", ES, Position.MID, 85, 2026),
            P("Nico Williams", "España", ES, Position.FWD, 86, 2026, true),
            P("Álvaro Morata", "España", ES, Position.FWD, 82, 2026),
            P("Ferran Torres", "España", ES, Position.FWD, 82, 2026),

            // ---- INGLATERRA ----
            P("Jordan Pickford", "Inglaterra", EN, Position.GK, 84, 2026),
            P("John Stones", "Inglaterra", EN, Position.DEF, 85, 2026),
            P("Kyle Walker", "Inglaterra", EN, Position.DEF, 84, 2022),
            P("Trent Alexander-Arnold", "Inglaterra", EN, Position.DEF, 85, 2026),
            P("Declan Rice", "Inglaterra", EN, Position.MID, 86, 2026, true),
            P("Phil Foden", "Inglaterra", EN, Position.MID, 87, 2026, true),
            P("Bukayo Saka", "Inglaterra", EN, Position.FWD, 87, 2026, true),
            P("Jack Grealish", "Inglaterra", EN, Position.FWD, 83, 2022),
            P("Marcus Rashford", "Inglaterra", EN, Position.FWD, 84, 2022),
            P("Cole Palmer", "Inglaterra", EN, Position.MID, 85, 2026),

            // ---- PORTUGAL ----
            P("Diogo Costa", "Portugal", PT, Position.GK, 85, 2026),
            P("Rúben Dias", "Portugal", PT, Position.DEF, 88, 2026, true),
            P("João Cancelo", "Portugal", PT, Position.DEF, 85, 2022),
            P("Vitinha", "Portugal", PT, Position.MID, 86, 2026, true),
            P("Rafael Leão", "Portugal", PT, Position.FWD, 86, 2026, true),
            P("Diogo Jota", "Portugal", PT, Position.FWD, 84, 2022),
            P("João Félix", "Portugal", PT, Position.FWD, 83, 2022),
            P("Gonçalo Ramos", "Portugal", PT, Position.FWD, 82, 2026),

            // ---- PAÍSES BAJOS ----
            P("Matthijs de Ligt", "P. Bajos", NL, Position.DEF, 85, 2022),
            P("Denzel Dumfries", "P. Bajos", NL, Position.DEF, 82, 2022),
            P("Nathan Aké", "P. Bajos", NL, Position.DEF, 82, 2022),
            P("Frenkie de Jong", "P. Bajos", NL, Position.MID, 87, 2026, true),
            P("Cody Gakpo", "P. Bajos", NL, Position.FWD, 85, 2026),
            P("Memphis Depay", "P. Bajos", NL, Position.FWD, 84, 2022),
            P("Xavi Simons", "P. Bajos", NL, Position.MID, 84, 2026),

            // ---- ITALIA ----
            P("Gianluigi Donnarumma", "Italia", IT, Position.GK, 88, 2026, true),
            P("Alessandro Bastoni", "Italia", IT, Position.DEF, 85, 2026),
            P("Giovanni Di Lorenzo", "Italia", IT, Position.DEF, 83, 2022),
            P("Nicolò Barella", "Italia", IT, Position.MID, 86, 2026, true),
            P("Jorginho", "Italia", IT, Position.MID, 84, 2022),
            P("Sandro Tonali", "Italia", IT, Position.MID, 84, 2026),
            P("Federico Chiesa", "Italia", IT, Position.FWD, 84, 2022),
            P("Gianluca Scamacca", "Italia", IT, Position.FWD, 81, 2026),

            // ---- BÉLGICA ----
            P("Romelu Lukaku", "Bélgica", BE, Position.FWD, 85, 2022),
            P("Youri Tielemans", "Bélgica", BE, Position.MID, 83, 2022),
            P("Jérémy Doku", "Bélgica", BE, Position.FWD, 84, 2026),
            P("Amadou Onana", "Bélgica", BE, Position.MID, 82, 2026),

            // ---- CROACIA ----
            P("Dominik Livaković", "Croacia", HR, Position.GK, 83, 2022),
            P("Joško Gvardiol", "Croacia", HR, Position.DEF, 86, 2026, true),
            P("Mateo Kovačić", "Croacia", HR, Position.MID, 85, 2022),
            P("Marcelo Brozović", "Croacia", HR, Position.MID, 84, 2022),
            P("Andrej Kramarić", "Croacia", HR, Position.FWD, 82, 2022),

            // ---- URUGUAY ----
            P("Ronald Araújo", "Uruguay", UY, Position.DEF, 85, 2026, true),
            P("José María Giménez", "Uruguay", UY, Position.DEF, 83, 2022),
            P("Rodrigo Bentancur", "Uruguay", UY, Position.MID, 83, 2026),
            P("Manuel Ugarte", "Uruguay", UY, Position.MID, 82, 2026),
            P("Darwin Núñez", "Uruguay", UY, Position.FWD, 84, 2026),

            // ---- COLOMBIA ----
            P("Dávinson Sánchez", "Colombia", CO, Position.DEF, 82, 2026),
            P("Daniel Muñoz", "Colombia", CO, Position.DEF, 81, 2026),
            P("Jefferson Lerma", "Colombia", CO, Position.MID, 81, 2026),
            P("Luis Díaz", "Colombia", CO, Position.FWD, 87, 2026, true),
            P("Jhon Durán", "Colombia", CO, Position.FWD, 81, 2026),

            // ---- VENEZUELA (la Vinotinto) ----
            P("Rafael Romo", "Venezuela", VE, Position.GK, 76, 2026),
            P("Wuilker Faríñez", "Venezuela", VE, Position.GK, 75, 2022),
            P("Nahuel Ferraresi", "Venezuela", VE, Position.DEF, 76, 2026),
            P("Yordan Osorio", "Venezuela", VE, Position.DEF, 75, 2026),
            P("Jon Aramburu", "Venezuela", VE, Position.DEF, 75, 2026),
            P("Wilker Ángel", "Venezuela", VE, Position.DEF, 73, 2022),
            P("Miguel Navarro", "Venezuela", VE, Position.DEF, 73, 2026),
            P("Yangel Herrera", "Venezuela", VE, Position.MID, 79, 2026, true),
            P("Tomás Rincón", "Venezuela", VE, Position.MID, 76, 2018),
            P("Cristian Cásseres Jr.", "Venezuela", VE, Position.MID, 75, 2026),
            P("Jefferson Savarino", "Venezuela", VE, Position.MID, 79, 2026, true),
            P("Yeferson Soteldo", "Venezuela", VE, Position.FWD, 78, 2026),
            P("Salomón Rondón", "Venezuela", VE, Position.FWD, 79, 2022, true),
            P("Josef Martínez", "Venezuela", VE, Position.FWD, 77, 2018),
            P("Darwin Machís", "Venezuela", VE, Position.FWD, 76, 2022),
            P("Eduard Bello", "Venezuela", VE, Position.FWD, 73, 2026),
        });

        const string GE = "🇬🇪", TR = "🇹🇷";

        // ============================================================
        //  ESTRELLAS DEL MUNDIAL 2026 + más leyendas por país/años
        // ============================================================
        l.AddRange(new[]
        {
            // ---- Cracks del Mundial 2026 ----
            P("Khvicha Kvaratskhelia", "Georgia", GE, Position.FWD, 87, 2026, true),
            P("Victor Osimhen", "Nigeria", NG, Position.FWD, 87, 2026, true),
            P("Désiré Doué", "Francia", FR, Position.FWD, 84, 2026),
            P("João Neves", "Portugal", PT, Position.MID, 85, 2026),
            P("Arda Güler", "Turquía", TR, Position.MID, 84, 2026),
            P("Warren Zaïre-Emery", "Francia", FR, Position.MID, 82, 2026),
            P("Estêvão", "Brasil", BR, Position.FWD, 83, 2026),
            P("Nico Paz", "Argentina", AR, Position.MID, 83, 2026),
            P("Alejandro Garnacho", "Argentina", AR, Position.FWD, 83, 2026),
            P("Pau Cubarsí", "España", ES, Position.DEF, 83, 2026),
            P("Kobbie Mainoo", "Inglaterra", EN, Position.MID, 82, 2026),
            P("Rasmus Højlund", "Dinamarca", DK, Position.FWD, 82, 2026),
            P("Mohammed Kudus", "Ghana", GH, Position.MID, 84, 2026),

            // ---- Venezuela ----
            P("Juan Arango", "Venezuela", VE, Position.MID, 85, 2010, true),
            P("Roberto Rosales", "Venezuela", VE, Position.DEF, 75, 2018),
            P("Giancarlo Maldonado", "Venezuela", VE, Position.FWD, 74, 2010),

            // ---- Más leyendas por país / años ----
            P("Paulo Roberto Falcão", "Brasil", BR, Position.MID, 87, 1982, true),
            P("Raí", "Brasil", BR, Position.MID, 85, 1994),
            P("Ricardo Bochini", "Argentina", AR, Position.MID, 85, 1986),
            P("Claudio Caniggia", "Argentina", AR, Position.FWD, 84, 1990),
            P("Gigi Riva", "Italia", IT, Position.FWD, 87, 1970, true),
            P("Sandro Mazzola", "Italia", IT, Position.MID, 85, 1970),
            P("Uwe Seeler", "Alemania", DE, Position.FWD, 85, 1970),
            P("Sepp Maier", "Alemania", DE, Position.GK, 90, 1974),
            P("Alan Shearer", "Inglaterra", EN, Position.FWD, 86, 1996, true),
            P("Andoni Zubizarreta", "España", ES, Position.GK, 84, 1994),
            P("Marius Trésor", "Francia", FR, Position.DEF, 83, 1978),
            P("Nani", "Portugal", PT, Position.FWD, 83, 2010),
            P("Obdulio Varela", "Uruguay", UY, Position.DEF, 85, 1950, true),
            P("Álvaro Recoba", "Uruguay", UY, Position.MID, 83, 2002),
            P("Faustino Asprilla", "Colombia", CO, Position.FWD, 84, 1994),
            P("Freddy Rincón", "Colombia", CO, Position.MID, 83, 1990),
            P("Cuauhtémoc Blanco", "México", MX, Position.MID, 83, 1998),
            P("Jorge Campos", "México", MX, Position.GK, 82, 1994),
            P("Marcelo Salas", "Chile", CL, Position.FWD, 84, 1998),
            P("Elías Figueroa", "Chile", CL, Position.DEF, 90, 1974, true),
        });

        // ---------- AGENTES LIBRES (baratos, para armar de cero) ----------
        string[] fn = { "M. Delgado", "R. Ferreira", "K. Novak", "J. Silva", "A. Costa",
                        "D. Petrov", "L. Moreau", "T. Andersen", "S. Kovač", "P. Rossi",
                        "N. Adebayo", "V. Ivanov", "C. Mendes", "H. Tanaka", "O. Bauer",
                        "F. Romano", "G. Larsen", "B. Osei", "E. Vargas", "W. Fischer",
                        "Q. Dubois", "Z. Marković", "Y. Kim", "U. Schneider" };
        var poss = new[] { Position.GK, Position.GK, Position.DEF, Position.DEF, Position.DEF,
                           Position.DEF, Position.MID, Position.MID, Position.MID, Position.MID,
                           Position.FWD, Position.FWD, Position.FWD, Position.DEF, Position.MID,
                           Position.FWD, Position.DEF, Position.MID, Position.FWD, Position.GK,
                           Position.DEF, Position.MID, Position.FWD, Position.DEF };
        int[] rt = { 58, 61, 60, 63, 66, 62, 64, 67, 69, 65,
                     63, 66, 70, 68, 71, 72, 59, 60, 62, 64,
                     66, 68, 70, 61 };
        for (int i = 0; i < fn.Length; i++)
            l.Add(P(fn[i], "Agente Libre", "🌐", poss[i], rt[i], 2026));


        // ============================================================
        //  LEYENDAS DE TODA LA HISTORIA
        //  Arqueros, defensas, volantes y delanteros de elite, para que
        //  el 90+ este bien representado en TODOS los puestos.
        // ============================================================
        const string AT = "🇦🇹", CA = "🇨🇦", CR = "🇨🇷", CZ = "🇨🇿", HU = "🇭🇺", IE = "🇮🇪", LR = "🇱🇷", MA = "🇲🇦", NIR = "🏴", PY = "🇵🇾", RS = "🇷🇸", SCO = "🏴", SI = "🇸🇮";

        l.AddRange(new[]
        {
            P("Ricardo Zamora", "España", ES, Position.GK, 90, 1934, true),
            P("Gilmar", "Brasil", BR, Position.GK, 90, 1958, true),
            P("Peter Shilton", "Inglaterra", EN, Position.GK, 90, 1990, true),
            P("Petr Čech", "Chequia", CZ, Position.GK, 90, 2006, true),
            P("Amadeo Carrizo", "Argentina", AR, Position.GK, 89, 1958, true),
            P("Ladislao Mazurkiewicz", "Uruguay", UY, Position.GK, 89, 1970, true),
            P("Rinat Dasayev", "Rusia", RU, Position.GK, 89, 1986, true),
            P("José Luis Chilavert", "Paraguay", PY, Position.GK, 89, 1998, true),
            P("Jan Oblak", "Eslovenia", SI, Position.GK, 89, 2022),
            P("José Ángel Iribar", "España", ES, Position.GK, 88, 1970, true),
            P("Pat Jennings", "Irlanda del Norte", NIR, Position.GK, 88, 1982, true),
            P("Jean-Marie Pfaff", "Bélgica", BE, Position.GK, 88, 1986, true),
            P("Michel Preud'homme", "Bélgica", BE, Position.GK, 88, 1994, true),
            P("Walter Zenga", "Italia", IT, Position.GK, 88, 1990, true),
            P("Claudio Taffarel", "Brasil", BR, Position.GK, 87, 1994, true),
            P("Fabien Barthez", "Francia", FR, Position.GK, 87, 1998, true),
            P("Keylor Navas", "Costa Rica", CR, Position.GK, 87, 2018),
            P("Yassine Bounou", "Marruecos", MA, Position.GK, 87, 2022),
            P("Antonio Carbajal", "México", MX, Position.GK, 86, 1962, true),
            P("Jan Tomaszewski", "Polonia", PL, Position.GK, 86, 1974, true),
            P("Hugo Gatti", "Argentina", AR, Position.GK, 86, 1978, true),
            P("Nery Pumpido", "Argentina", AR, Position.GK, 86, 1986, true),
            P("Óscar Córdoba", "Colombia", CO, Position.GK, 86, 2001, true),
            P("Carlos Alberto Torres", "Brasil", BR, Position.DEF, 91, 1970, true),
            P("Djalma Santos", "Brasil", BR, Position.DEF, 90, 1958, true),
            P("Gaetano Scirea", "Italia", IT, Position.DEF, 90, 1982, true),
            P("Ruud Krol", "Países Bajos", NL, Position.DEF, 89, 1974, true),
            P("José Santamaría", "Uruguay", UY, Position.DEF, 88, 1954, true),
            P("José Nasazzi", "Uruguay", UY, Position.DEF, 88, 1930, true),
            P("Júnior", "Brasil", BR, Position.DEF, 88, 1982, true),
            P("Hilderaldo Bellini", "Brasil", BR, Position.DEF, 87, 1958, true),
            P("Berti Vogts", "Alemania", DE, Position.DEF, 87, 1974, true),
            P("Andreas Brehme", "Alemania", DE, Position.DEF, 87, 1990, true),
            P("Fernando Hierro", "España", ES, Position.DEF, 87, 1998, true),
            P("Jaap Stam", "Países Bajos", NL, Position.DEF, 87, 1998, true),
            P("Lúcio", "Brasil", BR, Position.DEF, 87, 2002, true),
            P("Nemanja Vidić", "Serbia", RS, Position.DEF, 87, 2010),
            P("Vincent Kompany", "Bélgica", BE, Position.DEF, 87, 2014),
            P("Mats Hummels", "Alemania", DE, Position.DEF, 87, 2014),
            P("David Alaba", "Austria", AT, Position.DEF, 87, 2018),
            P("Claudio Gentile", "Italia", IT, Position.DEF, 86, 1982, true),
            P("Antonio Cabrini", "Italia", IT, Position.DEF, 86, 1982, true),
            P("Jürgen Kohler", "Alemania", DE, Position.DEF, 86, 1990, true),
            P("Alessandro Costacurta", "Italia", IT, Position.DEF, 86, 1994, true),
            P("Aldair", "Brasil", BR, Position.DEF, 86, 1994, true),
            P("Achraf Hakimi", "Marruecos", MA, Position.DEF, 85, 2022),
            P("Alphonso Davies", "Canada", CA, Position.DEF, 85, 2022),
            P("Juan Alberto Schiaffino", "Uruguay", UY, Position.MID, 90, 1950, true),
            P("Didi", "Brasil", BR, Position.MID, 90, 1958, true),
            P("Rivelino", "Brasil", BR, Position.MID, 90, 1970, true),
            P("Falcão", "Brasil", BR, Position.MID, 90, 1982, true),
            P("Gérson", "Brasil", BR, Position.MID, 89, 1970, true),
            P("Josef Masopust", "Chequia", CZ, Position.MID, 89, 1962, true),
            P("Luis Suárez Miramontes", "España", ES, Position.MID, 89, 1964, true),
            P("Gianni Rivera", "Italia", IT, Position.MID, 89, 1970, true),
            P("Frank Lampard", "Inglaterra", EN, Position.MID, 88, 2006, true),
            P("Paul Scholes", "Inglaterra", EN, Position.MID, 88, 2002, true),
            P("Clarence Seedorf", "Países Bajos", NL, Position.MID, 88, 2000, true),
            P("David Silva", "España", ES, Position.MID, 88, 2012),
            P("Mário Coluna", "Portugal", PT, Position.MID, 87, 1966, true),
            P("Roy Keane", "Irlanda", IE, Position.MID, 87, 2002, true),
            P("Deco", "Portugal", PT, Position.MID, 87, 2006, true),
            P("Michael Ballack", "Alemania", DE, Position.MID, 87, 2002, true),
            P("Edgar Davids", "Países Bajos", NL, Position.MID, 86, 1998, true),
            P("Ferenc Puskás", "Hungría", HU, Position.FWD, 94, 1954, true),
            P("George Best", "Irlanda del Norte", NIR, Position.FWD, 92, 1968, true),
            P("Giuseppe Meazza", "Italia", IT, Position.FWD, 91, 1934, true),
            P("Sándor Kocsis", "Hungría", HU, Position.FWD, 90, 1954, true),
            P("Jairzinho", "Brasil", BR, Position.FWD, 90, 1970, true),
            P("Josef Bican", "Austria", AT, Position.FWD, 90, 1938, true),
            P("Vinícius Júnior", "Brasil", BR, Position.FWD, 90, 2026),
            P("Zizinho", "Brasil", BR, Position.FWD, 89, 1950, true),
            P("Ademir", "Brasil", BR, Position.FWD, 89, 1950, true),
            P("Leônidas da Silva", "Brasil", BR, Position.FWD, 89, 1938, true),
            P("Silvio Piola", "Italia", IT, Position.FWD, 89, 1938, true),
            P("Omar Sívori", "Argentina", AR, Position.FWD, 89, 1962, true),
            P("George Weah", "Liberia", LR, Position.FWD, 89, 1996, true),
            P("Luis Figo", "Portugal", PT, Position.FWD, 89, 2000, true),
            P("Francisco Gento", "España", ES, Position.FWD, 88, 1964, true),
            P("Denis Law", "Escocia", SCO, Position.FWD, 88, 1974, true),
            P("Jimmy Greaves", "Inglaterra", EN, Position.FWD, 88, 1966, true),
            P("Oleg Blokhin", "Ucrania", UA, Position.FWD, 88, 1982, true),
            P("Raúl", "España", ES, Position.FWD, 88, 2002, true),
            P("Rudi Völler", "Alemania", DE, Position.FWD, 87, 1990, true),
            P("Christian Vieri", "Italia", IT, Position.FWD, 87, 2002, true),
            P("Hernán Crespo", "Argentina", AR, Position.FWD, 87, 2002, true),
            P("Fernando Torres", "España", ES, Position.FWD, 87, 2008, true),
        });

        return l;
    }
}
