namespace OnceDeOro.Models;

/// <summary>Foto de un rival dentro del calendario de un torneo (serializable).</summary>
public sealed class RivalSnapshot
{
    public string Name { get; set; } = "";
    public string Flag { get; set; } = "";
    public int Strength { get; set; }
}

/// <summary>
/// Un jugador que creaste vos en la academia y que sube de nivel entrenando y jugando.
/// Es el sumidero de dinero del juego avanzado: entrenar cuesta cada vez más caro.
/// </summary>
public sealed class AcademyPlayer
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Nation { get; set; } = "";
    public string Flag { get; set; } = "🎓";
    public Position Pos { get; set; }
    public int Rating { get; set; } = 55;

    /// <summary>Techo al que puede llegar entrenando. 99 = sin techo conocido.</summary>
    public int Potential { get; set; } = 99;
    /// <summary>Edad al ser descubierto (las promesas son muy jóvenes).</summary>
    public int Age { get; set; } = 18;

    /// <summary>Experiencia acumulada hacia el próximo punto de fuerza.</summary>
    public int Xp { get; set; }
    public int Sessions { get; set; }
    public int Matches { get; set; }
}

/// <summary>
/// Un jugador que te salió de una caja sorpresa. No existe en la base del mercado:
/// se genera al abrir la caja y vive acá, guardado con tu partida.
/// </summary>
public sealed class LootPlayer
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Nation { get; set; } = "";
    public string Flag { get; set; } = "🎁";
    public Position Pos { get; set; }
    public int Rating { get; set; }
    /// <summary>El 🤡 de la caja garantizada: 110 de cartel, un desastre jugando.</summary>
    public bool Troll { get; set; }
    /// <summary>De qué caja salió (se muestra en su ficha).</summary>
    public string BoxName { get; set; } = "";

    /// <summary>Ya se lo mejoró con su versión común. Se puede una sola vez.</summary>
    public bool Fused { get; set; }
}

/// <summary>
/// El "vos" del juego: un avatar 2D estilizado, tipo Mii pero plano. Cada campo
/// es un índice o un color; el dibujo lo arma AvatarView a partir de esto.
/// </summary>
public sealed class Avatar
{
    public bool Creado { get; set; }          // false = todavía no lo armaste
    public string Name { get; set; } = "";

    public int Face { get; set; }             // forma de la cara
    public string Skin { get; set; } = "#f1c9a5";
    public int Hair { get; set; }             // peinado
    public string HairColor { get; set; } = "#2b2016";
    public int Eyes { get; set; }
    public string EyeColor { get; set; } = "#4a3524";
    public int Brows { get; set; }
    public int Nose { get; set; }
    public int Mouth { get; set; }
    public bool Beard { get; set; }
    public bool Glasses { get; set; }
    public string GlassesColor { get; set; } = "#1a1a1a";
}

/// <summary>
/// Quién se hace cargo de cada cosa en la cancha. Vacío = lo elige el simulador
/// solo, como hacía antes (el mejor disponible para esa jugada).
/// </summary>
public sealed class TeamRoles
{
    public string CaptainId { get; set; } = "";
    public string PenaltyId { get; set; } = "";
    public string FreeKickId { get; set; } = "";
    /// <summary>Córners por la banda derecha (y por la izquierda).</summary>
    public string CornerRightId { get; set; } = "";
    public string CornerLeftId { get; set; } = "";
}

/// <summary>
/// Estado físico e historial de UN jugador tuyo: lo que se acumula partido a partido.
/// </summary>
public sealed class PlayerCondition
{
    /// <summary>0 = entero, 100 = fundido. Resta rendimiento y aumenta el riesgo de lesión.</summary>
    public int Fatigue { get; set; }

    /// <summary>Partidos que le faltan para volver. 0 = disponible.</summary>
    public int OutMatches { get; set; }

    /// <summary>
    /// Si está concentrado descansando, cuándo termina (UTC). Mientras tanto no
    /// puede jugar; al cumplirse vuelve con el tanque lleno. Se guarda como
    /// instante absoluto para que el descanso corra aunque cierres el juego.
    /// </summary>
    public DateTime? RestUntil { get; set; }

    // ---- Estadísticas acumuladas con vos ----
    public int Matches { get; set; }
    public int Goals { get; set; }
    public int Yellow { get; set; }
    public int Red { get; set; }
    public int Injuries { get; set; }

    /// <summary>
    /// Partidos de suspensión que le quedan por cumplir. Va aparte de OutMatches
    /// porque un expulsado NO está lesionado: la ficha tiene que decir la verdad.
    /// </summary>
    public int Suspended { get; set; }

    /// <summary>Amarillas acumuladas desde la última suspensión (cada 5, se pierde un partido).</summary>
    public int YellowStreak { get; set; }

    public bool Injured => OutMatches > 0;
}

/// <summary>
/// Una joven promesa que encontró un ojeador. Todavía no es tuya: hay que ficharla.
/// Lo valioso no es lo que rinde hoy, sino hasta dónde puede llegar.
/// </summary>
public sealed class Prospect
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Nation { get; set; } = "";
    public string Flag { get; set; } = "🌱";
    public Position Pos { get; set; }
    public int Rating { get; set; }
    /// <summary>El techo al que puede llegar entrenando.</summary>
    public int Potential { get; set; }
    public int Age { get; set; }
    public int Cost { get; set; }
    /// <summary>Qué ojeador lo encontró.</summary>
    public string ScoutName { get; set; } = "";
}

/// <summary>Una fila de la tabla de posiciones de una liga.</summary>
public sealed class TableRow
{
    public string Name { get; set; } = "";
    public string Flag { get; set; } = "";
    public int Strength { get; set; }
    public bool IsMe { get; set; }
    public int Played { get; set; }
    public int Won { get; set; }
    public int Drawn { get; set; }
    public int Lost { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }

    public int Points => Won * 3 + Drawn;
    public int Diff => GoalsFor - GoalsAgainst;
}

/// <summary>Estado serializable de una participación en un torneo.</summary>
public sealed class RunState
{
    /// <summary>Tabla de posiciones (sólo en ligas). Incluye tu equipo y a los rivales.</summary>
    public List<TableRow> Table { get; set; } = new();

    public string CompId { get; set; } = "";
    public int Stage { get; set; }
    public int GroupPoints { get; set; }
    public int GroupPlayed { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public bool Eliminated { get; set; }
    public bool Champion { get; set; }
    public bool CleanRun { get; set; } = true;
    public bool PerfectRun { get; set; } = true;
    public int MoneyWon { get; set; }
    public List<RivalSnapshot> Schedule { get; set; } = new();
    public List<string> Timeline { get; set; } = new();
}

/// <summary>Todo el progreso persistente del jugador. Se guarda en localStorage.</summary>
public sealed class GameState
{
    public int Money { get; set; } = 200;
    public string ClubName { get; set; } = "Once de Oro FC";
    /// <summary>Color principal de la camiseta (y de la hinchada en las gradas).</summary>
    public string Primary { get; set; } = "#f5c542";
    /// <summary>Color secundario, para el contraste de la camiseta y las gradas.</summary>
    public string Secondary { get; set; } = "#12203c";
    /// <summary>Estilo de las gradas del estadio (ver GameService.StandStyles).</summary>
    public string Stands { get; set; } = "clasica";

    /// <summary>Tamaño del estadio comprado (ver GameService.StadiumTiers).</summary>
    public string Stadium { get; set; } = "popular";

    /// <summary>
    /// Recaudación en MILES que todavía no llegó a completar un millón. El dinero
    /// del juego se lleva en millones enteros, y la tribuna más chica recauda 500
    /// mil por partido: sin este resto, esa entrada se perdía al redondear.
    /// </summary>
    public int GateBankK { get; set; }
    public string FormationName { get; set; } = "4-3-3";
    public TeamStyle Style { get; set; } = TeamStyle.Equilibrado;

    public List<string> OwnedIds { get; set; } = new();
    public List<string> StartingIds { get; set; } = new();

    /// <summary>Jugadores creados y entrenados por vos en la academia.</summary>
    public List<AcademyPlayer> Academy { get; set; } = new();

    /// <summary>Cansancio, lesiones y estadísticas de cada jugador, por Id.</summary>
    public Dictionary<string, PlayerCondition> Conditions { get; set; } = new();

    /// <summary>Tokens de mejora disponibles para subir de nivel a cualquier jugador.</summary>
    public int Tokens { get; set; }

    /// <summary>Tu avatar 2D (el DT). Se muestra en la barra de arriba.</summary>
    public Avatar Avatar { get; set; } = new();

    /// <summary>Quién patea qué y quién lleva la cinta.</summary>
    public TeamRoles Roles { get; set; } = new();

    /// <summary>Orden táctica de cada jugador, por Id (ver GameService.Ordenes).</summary>
    public Dictionary<string, string> Orders { get; set; } = new();

    /// <summary>Jugadores que te salieron de las cajas sorpresa.</summary>
    public List<LootPlayer> Loot { get; set; } = new();

    /// <summary>Promesas que encontraron tus ojeadores y todavía podés fichar.</summary>
    public List<Prospect> Prospects { get; set; } = new();

    /// <summary>Puntos de fuerza ganados con tokens, por jugador.</summary>
    public Dictionary<string, int> Upgrades { get; set; } = new();

    // Estadísticas de carrera
    public int MatchesPlayed { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int BestRatingReached { get; set; }
    public List<string> Honours { get; set; } = new();   // trofeos ganados
    public List<string> History { get; set; } = new();    // historial de torneos jugados
    public bool AchievedSevenZero { get; set; }           // logro legendario

    public RunState? Run { get; set; }

    public bool HasSeenIntro { get; set; }
}
