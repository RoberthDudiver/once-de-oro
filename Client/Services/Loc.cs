using Microsoft.JSInterop;

namespace OnceDeOro.Services;

/// <summary>
/// Localización simple: la CLAVE es el texto en español. Para inglés/portugués se
/// busca en el diccionario; si falta, cae al español. Idioma persistido en localStorage.
/// </summary>
public sealed class Loc
{
    private const string Key = "once-de-oro:lang:v1";
    private readonly IJSRuntime _js;
    public Loc(IJSRuntime js) => _js = js;

    public string Lang { get; private set; } = "es";
    public event Action? Changed;

    public static readonly (string Code, string Flag, string Name)[] Languages =
    {
        ("es", "🇪🇸", "Español"),
        ("en", "🇬🇧", "English"),
        ("pt", "🇧🇷", "Português"),
    };

    public async Task LoadAsync()
    {
        try
        {
            var v = await _js.InvokeAsync<string?>("localStorage.getItem", Key);
            if (v is "es" or "en" or "pt") Lang = v;
        }
        catch { }
        Changed?.Invoke();
    }

    public async Task SetLangAsync(string lang)
    {
        if (lang is not ("es" or "en" or "pt") || lang == Lang) return;
        Lang = lang;
        try { await _js.InvokeVoidAsync("localStorage.setItem", Key, lang); } catch { }
        Changed?.Invoke();
    }

    /// <summary>Traduce (clave = español). Uso: Loc.T("Mercado de fichajes").</summary>
    public string T(string es)
    {
        if (Lang == "es") return es;
        var dict = Lang == "en" ? En : Pt;
        return dict.TryGetValue(es, out var v) ? v : es;
    }

    // Índice cómodo: @Loc["texto"]
    public string this[string es] => T(es);

    private static readonly Dictionary<string, string> En = new()
    {
        // Navegación / topbar
        ["Inicio"] = "Home",
        ["Mercado"] = "Market",
        ["Equipo"] = "Squad",
        ["Torneos"] = "Cups",
        ["Online"] = "Online",
        ["Once de Oro · Manager"] = "Once de Oro · Manager",
        ["Presupuesto disponible"] = "Available budget",
        ["Idioma"] = "Language",
        // Home
        ["★ Temporada abierta"] = "★ Season open",
        ["Empezás de cero con tu presupuesto. Fichá leyendas de todos los Mundiales, ganá torneos, cobrá premios y volvé al mercado por más estrellas. La meta legendaria: ser campeón sin recibir un solo gol."]
            = "Start from scratch with your budget. Sign legends from every World Cup, win cups, collect prizes and return to the market for more stars. The legendary goal: be champion without conceding a single goal.",
        ["▶ Continuar torneo"] = "▶ Continue cup",
        ["🏆 Inscribirse a un torneo"] = "🏆 Enter a cup",
        ["💰 Ir al mercado de fichajes"] = "💰 Go to the transfer market",
        ["👕 Ver mi equipo"] = "👕 View my squad",
        ["Estado del club"] = "Club status",
        ["Fuerza del equipo"] = "Team strength",
        ["XI TITULAR COMPLETO"] = "FULL STARTING XI",
        ["DE CANTERA"] = "YOUTH",
        ["Palmarés"] = "Honours",
        ["Todavía sin trofeos."] = "No trophies yet.",
        ["¡Salí a ganar tu primer título!"] = "Go win your first title!",
        ["⭐ CAMPEÓN INVICTO SIN RECIBIR GOLES"] = "⭐ UNBEATEN CHAMPION, NO GOALS CONCEDED",
        ["Carrera"] = "Career",
        ["Partidos"] = "Matches",
        ["Ganados"] = "Won",
        ["Empates"] = "Draws",
        ["Perdidos"] = "Lost",
        ["Goles a favor"] = "Goals for",
        ["En contra"] = "Against",
        ["Historial de torneos"] = "Cup history",
        ["↺ Reiniciar carrera"] = "↺ Reset career",
        ["jugadores"] = "players",
        // Mercado
        ["Mercado de fichajes"] = "Transfer market",
        ["💰 Comprar"] = "💰 Buy",
        ["👕 Mi plantel"] = "👕 My squad",
        ["Todos"] = "All",
        ["🔎 Buscar jugador o selección…"] = "🔎 Search player or country…",
        ["🌍 Todos los países"] = "🌍 All countries",
        ["📅 Todas las épocas"] = "📅 All eras",
        ["✕ Limpiar"] = "✕ Clear",
        ["No hay jugadores con ese filtro."] = "No players match that filter.",
        ["Tu plantel está vacío. Andá a Comprar para fichar."] = "Your squad is empty. Go to Buy to sign players.",
        // Equipo
        ["Mi equipo"] = "My squad",
        ["No tenés jugadores todavía."] = "You don't have any players yet.",
        ["💰 Ir al mercado"] = "💰 Go to the market",
        ["Nombre del club"] = "Club name",
        ["Nombre de tu equipo"] = "Your team name",
        ["Formación"] = "Formation",
        ["Estilo"] = "Style",
        ["Defensivo"] = "Defensive",
        ["Equilibrado"] = "Balanced",
        ["Ofensivo"] = "Attacking",
        ["Fuerza"] = "Strength",
        ["⚡ Auto XI"] = "⚡ Auto XI",
        ["✓ XI TITULAR COMPLETO — LISTO PARA JUGAR"] = "✓ FULL STARTING XI — READY TO PLAY",
        ["Plantel"] = "Squad",
        ["tocá para poner/sacar del XI"] = "tap to add/remove from the XI",
        ["Cantera"] = "Youth",
        ["libre"] = "free",
        // Torneos
        ["Competiciones"] = "Competitions",
        ["Torneo en curso"] = "Cup in progress",
        ["▶ Continuar"] = "▶ Continue",
        ["Inscribirse"] = "Enter",
        ["Gratis"] = "Free",
        ["Tu fuerza"] = "Your strength",
        ["Premio campeón"] = "Champion prize",
        ["Inscripción"] = "Entry fee",
        ["Nivel"] = "Tier",
        ["⚠️ Necesitás un XI válido (11 titulares según tu formación) para inscribirte."] = "⚠️ You need a valid XI (11 starters for your formation) to enter.",
        ["Armar equipo"] = "Build squad",
        ["Ir al mercado"] = "Go to the market",
        // Partido
        ["No hay ningún torneo en curso."] = "There is no cup in progress.",
        ["Elegir torneo"] = "Choose a cup",
        ["PREVIA"] = "PREVIEW",
        ["📊 Pronóstico"] = "📊 Prediction",
        ["goles esperados"] = "expected goals",
        ["favoritismo"] = "favourite",
        ["Marcadores más probables:"] = "Most likely scores:",
        ["Ganás"] = "Win",
        ["Perdés"] = "Lose",
        ["Empate"] = "Draw",
        ["▶ Ver el partido"] = "▶ Watch the match",
        ["FINAL"] = "FULL TIME",
        ["¡VICTORIA!"] = "WIN!",
        ["EMPATE"] = "DRAW",
        ["DERROTA"] = "DEFEAT",
        ["en premios"] = "in prizes",
        ["¡CAMPEÓN!"] = "CHAMPION!",
        ["Cobrar y volver"] = "Collect and return",
        ["Volver al mercado"] = "Back to the market",
        ["Volver"] = "Back",
        ["Reforzá el equipo y volvé más fuerte."] = "Reinforce your squad and come back stronger.",
        // MatchField
        ["Posesión"] = "Possession",
        ["Remates"] = "Shots",
        ["Faltas"] = "Fouls",
        ["⏩ Saltar al final"] = "⏩ Skip to the end",
        ["1º TIEMPO"] = "1ST HALF",
        ["ENTRETIEMPO"] = "HALF TIME",
        ["2º TIEMPO"] = "2ND HALF",
        ["PRÓRROGA 1"] = "EXTRA TIME 1",
        ["PRÓRROGA 2"] = "EXTRA TIME 2",
        ["TANDA DE PENALES"] = "PENALTY SHOOTOUT",
        // Online
        ["Duelo online · jugá contra amigos"] = "Online duel · play against friends",
        ["Crear una sala"] = "Create a room",
        ["Unirse a una sala"] = "Join a room",
        ["Tu nombre"] = "Your name",
        ["Tu apodo"] = "Your nickname",
        ["Equipos"] = "Teams",
        ["Draft parejo"] = "Fair draft",
        ["Equipo de carrera"] = "Career squad",
        ["Formato"] = "Format",
        ["Único"] = "Single",
        ["Mejor de 3"] = "Best of 3",
        ["Mini-torneo"] = "Mini-cup",
        ["🌐 Crear sala"] = "🌐 Create room",
        ["Código de sala"] = "Room code",
        ["Entrar"] = "Join",
        ["Jugadores"] = "Players",
    };

    private static readonly Dictionary<string, string> Pt = new()
    {
        ["Inicio"] = "Início",
        ["Mercado"] = "Mercado",
        ["Equipo"] = "Elenco",
        ["Torneos"] = "Torneios",
        ["Online"] = "Online",
        ["Presupuesto disponible"] = "Orçamento disponível",
        ["Idioma"] = "Idioma",
        ["★ Temporada abierta"] = "★ Temporada aberta",
        ["Empezás de cero con tu presupuesto. Fichá leyendas de todos los Mundiales, ganá torneos, cobrá premios y volvé al mercado por más estrellas. La meta legendaria: ser campeón sin recibir un solo gol."]
            = "Comece do zero com seu orçamento. Contrate lendas de todas as Copas, vença torneios, receba prêmios e volte ao mercado por mais estrelas. A meta lendária: ser campeão sem sofrer um único gol.",
        ["▶ Continuar torneo"] = "▶ Continuar torneio",
        ["🏆 Inscribirse a un torneo"] = "🏆 Inscrever-se em um torneio",
        ["💰 Ir al mercado de fichajes"] = "💰 Ir ao mercado de transferências",
        ["👕 Ver mi equipo"] = "👕 Ver meu elenco",
        ["Estado del club"] = "Status do clube",
        ["Fuerza del equipo"] = "Força do time",
        ["XI TITULAR COMPLETO"] = "XI TITULAR COMPLETO",
        ["Palmarés"] = "Títulos",
        ["Todavía sin trofeos."] = "Ainda sem troféus.",
        ["¡Salí a ganar tu primer título!"] = "Vá conquistar seu primeiro título!",
        ["⭐ CAMPEÓN INVICTO SIN RECIBIR GOLES"] = "⭐ CAMPEÃO INVICTO SEM SOFRER GOLS",
        ["Carrera"] = "Carreira",
        ["Partidos"] = "Jogos",
        ["Ganados"] = "Vitórias",
        ["Empates"] = "Empates",
        ["Perdidos"] = "Derrotas",
        ["Goles a favor"] = "Gols pró",
        ["En contra"] = "Contra",
        ["Historial de torneos"] = "Histórico de torneios",
        ["↺ Reiniciar carrera"] = "↺ Reiniciar carreira",
        ["jugadores"] = "jogadores",
        ["Mercado de fichajes"] = "Mercado de transferências",
        ["💰 Comprar"] = "💰 Comprar",
        ["👕 Mi plantel"] = "👕 Meu elenco",
        ["Todos"] = "Todos",
        ["🔎 Buscar jugador o selección…"] = "🔎 Buscar jogador ou seleção…",
        ["🌍 Todos los países"] = "🌍 Todos os países",
        ["📅 Todas las épocas"] = "📅 Todas as épocas",
        ["✕ Limpiar"] = "✕ Limpar",
        ["No hay jugadores con ese filtro."] = "Nenhum jogador com esse filtro.",
        ["Tu plantel está vacío. Andá a Comprar para fichar."] = "Seu elenco está vazio. Vá em Comprar para contratar.",
        ["Mi equipo"] = "Meu elenco",
        ["No tenés jugadores todavía."] = "Você ainda não tem jogadores.",
        ["💰 Ir al mercado"] = "💰 Ir ao mercado",
        ["Nombre del club"] = "Nome do clube",
        ["Nombre de tu equipo"] = "Nome do seu time",
        ["Formación"] = "Formação",
        ["Estilo"] = "Estilo",
        ["Defensivo"] = "Defensivo",
        ["Equilibrado"] = "Equilibrado",
        ["Ofensivo"] = "Ofensivo",
        ["Fuerza"] = "Força",
        ["⚡ Auto XI"] = "⚡ Auto XI",
        ["✓ XI TITULAR COMPLETO — LISTO PARA JUGAR"] = "✓ XI TITULAR COMPLETO — PRONTO PARA JOGAR",
        ["Plantel"] = "Elenco",
        ["tocá para poner/sacar del XI"] = "toque para colocar/tirar do XI",
        ["Cantera"] = "Base",
        ["libre"] = "livre",
        ["Competiciones"] = "Competições",
        ["Torneo en curso"] = "Torneio em andamento",
        ["▶ Continuar"] = "▶ Continuar",
        ["Inscribirse"] = "Inscrever-se",
        ["Gratis"] = "Grátis",
        ["Tu fuerza"] = "Sua força",
        ["Premio campeón"] = "Prêmio campeão",
        ["Inscripción"] = "Inscrição",
        ["Nivel"] = "Nível",
        ["⚠️ Necesitás un XI válido (11 titulares según tu formación) para inscribirte."] = "⚠️ Você precisa de um XI válido (11 titulares para sua formação) para se inscrever.",
        ["Armar equipo"] = "Montar time",
        ["Ir al mercado"] = "Ir ao mercado",
        ["No hay ningún torneo en curso."] = "Não há nenhum torneio em andamento.",
        ["Elegir torneo"] = "Escolher torneio",
        ["PREVIA"] = "PRÉVIA",
        ["📊 Pronóstico"] = "📊 Prognóstico",
        ["goles esperados"] = "gols esperados",
        ["favoritismo"] = "favoritismo",
        ["Marcadores más probables:"] = "Placares mais prováveis:",
        ["Ganás"] = "Vitória",
        ["Perdés"] = "Derrota",
        ["Empate"] = "Empate",
        ["▶ Ver el partido"] = "▶ Assistir a partida",
        ["FINAL"] = "FIM DE JOGO",
        ["¡VICTORIA!"] = "VITÓRIA!",
        ["EMPATE"] = "EMPATE",
        ["DERROTA"] = "DERROTA",
        ["en premios"] = "em prêmios",
        ["¡CAMPEÓN!"] = "CAMPEÃO!",
        ["Cobrar y volver"] = "Receber e voltar",
        ["Volver al mercado"] = "Voltar ao mercado",
        ["Volver"] = "Voltar",
        ["Reforzá el equipo y volvé más fuerte."] = "Reforce o time e volte mais forte.",
        ["Posesión"] = "Posse",
        ["Remates"] = "Finalizações",
        ["Faltas"] = "Faltas",
        ["⏩ Saltar al final"] = "⏩ Pular para o fim",
        ["1º TIEMPO"] = "1º TEMPO",
        ["ENTRETIEMPO"] = "INTERVALO",
        ["2º TIEMPO"] = "2º TEMPO",
        ["PRÓRROGA 1"] = "PRORROGAÇÃO 1",
        ["PRÓRROGA 2"] = "PRORROGAÇÃO 2",
        ["TANDA DE PENALES"] = "DISPUTA DE PÊNALTIS",
        ["Duelo online · jugá contra amigos"] = "Duelo online · jogue contra amigos",
        ["Crear una sala"] = "Criar uma sala",
        ["Unirse a una sala"] = "Entrar em uma sala",
        ["Tu nombre"] = "Seu nome",
        ["Tu apodo"] = "Seu apelido",
        ["Equipos"] = "Times",
        ["Draft parejo"] = "Draft justo",
        ["Equipo de carrera"] = "Time de carreira",
        ["Formato"] = "Formato",
        ["Único"] = "Único",
        ["Mejor de 3"] = "Melhor de 3",
        ["Mini-torneo"] = "Mini-torneio",
        ["🌐 Crear sala"] = "🌐 Criar sala",
        ["Código de sala"] = "Código da sala",
        ["Entrar"] = "Entrar",
        ["Jugadores"] = "Jogadores",
    };
}
