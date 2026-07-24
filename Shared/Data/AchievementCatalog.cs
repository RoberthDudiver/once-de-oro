using OnceDeOro.Models;

namespace OnceDeOro.Data;

/// <summary>El catálogo de logros del juego, agrupados por nivel (bronce → platino).</summary>
public static class AchievementCatalog
{
    private static Achievement A(string id, string emoji, string name, string desc, AchievementTier tier)
        => new() { Id = id, Emoji = emoji, Name = name, Desc = desc, Tier = tier };

    public static readonly IReadOnlyList<Achievement> All = new List<Achievement>
    {
        // ---------------------------------------------------------------- Bronce
        A("first-match",   "🧑‍💼", "Bienvenido, Míster",     "Dirigí tu primer partido.",                         AchievementTier.Bronce),
        A("first-win",     "⚽",   "Primer Paso",            "Ganá tu primer partido.",                            AchievementTier.Bronce),
        A("first-signing", "💰",   "Buen Negocio",           "Hacé tu primer fichaje en el mercado.",              AchievementTier.Bronce),
        A("tactician",     "🎯",   "Táctico",                "Meté un cambio o cambiá el planteo en pleno partido.", AchievementTier.Bronce),

        // ---------------------------------------------------------------- Plata
        A("win-streak-5",     "🔥", "Racha Imparable",         "Ganá 5 partidos seguidos.",                         AchievementTier.Plata),
        A("wall-5",           "🛡️", "Muralla",                 "Mantené el arco en cero 5 partidos seguidos.",      AchievementTier.Plata),
        A("goal-machine-100", "⚽", "Máquina de Goles",        "Marcá 100 goles con un mismo club.",                AchievementTier.Plata),
        A("youth-debut",      "🌟", "Descubridor de Talentos", "Hacé debutar a un juvenil de tu academia.",         AchievementTier.Plata),

        // ---------------------------------------------------------------- Oro
        A("league-champ", "🏆", "Campeón de Liga",         "Ganá una liga.",                                    AchievementTier.Oro),
        A("cup-king",     "🏅", "Rey de Copas",            "Ganá una copa nacional.",                           AchievementTier.Oro),
        A("continental",  "🌍", "Conquista Internacional", "Ganá un torneo continental.",                       AchievementTier.Oro),
        A("treble",       "👑", "Triplete Histórico",      "Ganá liga, copa y torneo continental.",             AchievementTier.Oro),

        // ---------------------------------------------------------------- Platino (leyenda)
        A("world-champ",      "🏆",     "Campeón del Mundo",    "Ganá un Mundial.",                                        AchievementTier.Platino),
        A("messi-dream",      "🏆🏆",   "El Sueño de Messi",    "Ganá dos Mundiales seguidos.",                            AchievementTier.Platino),
        A("better-than-pele", "🏆🏆🏆", "Mejor que Pelé",       "Ganá tres Mundiales seguidos.",                           AchievementTier.Platino),
        A("youth-glory",      "🌟",     "Joya de la Cantera",   "Sé campeón con un juvenil de tu academia en el once.",    AchievementTier.Platino),
        A("legend-leader",    "👑",     "Líder de Leyendas",    "Ganá una Champions o un Mundial con un equipo de puras leyendas.", AchievementTier.Platino),
    };

    public static Achievement ById(string id) => All.First(a => a.Id == id);
    public static IEnumerable<Achievement> ByTier(AchievementTier t) => All.Where(a => a.Tier == t);
}
