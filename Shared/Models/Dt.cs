namespace OnceDeOro.Models;

/// <summary>Licencia del entrenador: define a qué clubes puede aspirar al arrancar.</summary>
public enum DtLicense { Basica, Avanzada, Profesional }

/// <summary>De dónde viene el DT (solo cambia su historia, sin ventajas directas).</summary>
public enum DtBackground { Exjugador, Cantera, Analista, Preparador, Estudiante }

/// <summary>Una noticia del mundo del fútbol.</summary>
public sealed class DtNews
{
    public string Icon { get; set; } = "📰";
    public string Text { get; set; } = "";
}

/// <summary>Un jugador que encontró el ojeador y se puede fichar.</summary>
public sealed class DtProspect
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Nation { get; set; } = "";
    public string NationCode { get; set; } = "";
    public Position Pos { get; set; }
    public int Age { get; set; }
    public int Media { get; set; }
    public int Potential { get; set; }
    public int ValueM { get; set; }
    public bool Free { get; set; }
    public string Club { get; set; } = "";
}

/// <summary>Un jugador de la plantilla del club que dirigís (Modo DT).</summary>
public sealed class DtPlayer
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Nation { get; set; } = "";
    public string NationCode { get; set; } = "";
    public Position Pos { get; set; }
    public int Age { get; set; }
    public int Media { get; set; }
    public int Potential { get; set; }
    public int Morale { get; set; } = 70;      // 0..100
    public int Fatigue { get; set; }           // 0..100
    public int InjuryWeeks { get; set; }       // >0 = lesionado
    public int ContractYears { get; set; } = 3;
    public int SalaryM { get; set; }
    public int ValueM { get; set; }
    public int Apps { get; set; }
    public int Goals { get; set; }
    public int Assists { get; set; }
}

/// <summary>Un partido del calendario del club.</summary>
public sealed class DtMatch
{
    public int Round { get; set; }
    public string Opp { get; set; } = "";
    public int OppStrength { get; set; }
    public bool Home { get; set; }
    public bool Played { get; set; }
    public int MyGoals { get; set; }
    public int OppGoals { get; set; }
}

/// <summary>Una semana del calendario: partido, entrenamiento, prensa o descanso.</summary>
public sealed class DtWeek
{
    public int Index { get; set; }
    public string Month { get; set; } = "";
    public string Type { get; set; } = "";   // match | train | press | rest
    public bool Done { get; set; }
}

/// <summary>Una fila de la tabla de la liga del Modo DT.</summary>
public sealed class DtTableRow
{
    public string Name { get; set; } = "";
    public int Strength { get; set; }
    public bool IsMe { get; set; }
    public int Played { get; set; }
    public int Won { get; set; }
    public int Drawn { get; set; }
    public int Lost { get; set; }
    public int GF { get; set; }
    public int GA { get; set; }
    public int Pts => Won * 3 + Drawn;
    public int Diff => GF - GA;
}

/// <summary>Un club dirigible en el Modo DT.</summary>
public sealed class DtClub
{
    public string Name { get; set; } = "";
    public string Emblem { get; set; } = "⚽";
    public string Country { get; set; } = "";
    public string League { get; set; } = "";
    public int Level { get; set; } = 1;      // 1 Pequeño · 2 Mediano · 3 Grande · 4 Gigante
    public int Strength { get; set; } = 62;
    public int BudgetM { get; set; }          // presupuesto de fichajes (M) que da la directiva
}

/// <summary>Un objetivo que pone la directiva para la temporada.</summary>
public sealed class DtObjective
{
    public string Text { get; set; } = "";
    public string Kind { get; set; } = "";   // liga | intl | copa | nodesc | media | juveniles
    public bool Met { get; set; }
}

/// <summary>Una temporada cerrada (fila del historial del DT).</summary>
public sealed class DtSeason
{
    public int Year { get; set; }
    public string Club { get; set; } = "";
    public int Pos { get; set; }
    public int Teams { get; set; }
    public int Played { get; set; }
    public int Won { get; set; }
    public int Drawn { get; set; }
    public int Lost { get; set; }
    public int GF { get; set; }
    public int GA { get; set; }
    public List<string> Titles { get; set; } = new();
    public bool ObjectiveMet { get; set; }
    public int RepAfter { get; set; }
    public string Note { get; set; } = "";
}

/// <summary>Una opción de una decisión de fin de temporada.</summary>
public sealed class DtOption
{
    public string Label { get; set; } = "";
    public string Sub { get; set; } = "";
    public string Kind { get; set; } = "";   // stay | move | retire | press
    public DtClub? Club { get; set; }
    public int MoraleDelta { get; set; }     // efecto en la moral del plantel (prensa)
    public int ConfDelta { get; set; }       // efecto en la confianza de la directiva (prensa)
}

public sealed class DtDecision
{
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public List<DtOption> Options { get; set; } = new();
}

/// <summary>Toda la carrera del entrenador (se guarda aparte).</summary>
public sealed class DtManager
{
    // ---- Identidad ----
    public string Name { get; set; } = "";
    public string Surname { get; set; } = "";
    public string Nation { get; set; } = "Argentina";
    public string NationCode { get; set; } = "ARG";
    public int Age { get; set; } = 35;
    public DtLicense License { get; set; } = DtLicense.Basica;
    public DtBackground Background { get; set; } = DtBackground.Exjugador;
    public List<string> Languages { get; set; } = new() { "Español" };

    // ---- Estado ----
    public bool Started { get; set; }
    public bool Employed { get; set; } = true;
    public bool Warned { get; set; }        // la directiva ya avisó que peligra el puesto
    public int Rep { get; set; }            // 0..100 (reputación)
    public int Confidence { get; set; } = 60; // 0..100 (confianza de la directiva)
    public int Year { get; set; } = 1;

    public DtClub? Club { get; set; }
    public List<DtObjective> Objectives { get; set; } = new();
    public DtDecision? Pending { get; set; }

    // ---- Economía (Pilar 2) ----
    public int CajaM { get; set; }              // caja del club (M)
    public int TransferBudgetM { get; set; }    // presupuesto de fichajes de la temporada (M)
    public int WageBudgetM { get; set; }        // presupuesto salarial (M/temporada, informativo)
    public int LastIncomeM { get; set; }        // ingresos de la última temporada
    public int LastExpenseM { get; set; }       // gastos de la última temporada
    public int LoanRemainingM { get; set; }     // deuda pendiente de préstamos
    public int LoanSeasonsLeft { get; set; }    // temporadas que faltan para saldarla
    public int SpentThisSeasonM { get; set; }   // ya reforzado esta temporada (para la UI)

    // Instalaciones (nivel 1..5). Suben con inversión y benefician a largo plazo.
    public int TrainCenter { get; set; } = 1;   // desarrollo del plantel
    public int Youth { get; set; } = 1;         // cantera
    public int Medical { get; set; } = 1;       // menos temporadas malas
    public int Scouting { get; set; } = 1;      // mejores informes y más candidatos

    // Mercado (Pilar 3)
    public List<DtProspect> ScoutPool { get; set; } = new();   // resultados de la última búsqueda
    public List<string> Signings { get; set; } = new();        // fichajes hechos (para la vitrina)

    // Mundo vivo (Pilar 4)
    public List<DtNews> News { get; set; } = new();            // feed de la última temporada
    public List<string> Awards { get; set; } = new();          // tus premios personales (DT del Año…)
    public List<string> WorldLog { get; set; } = new();        // campeón + Balón de Oro por temporada

    // Plantilla + calendario (semana a semana, pedido de Alexander)
    public List<DtPlayer> Squad { get; set; } = new();
    public List<DtMatch> Fixture { get; set; } = new();
    public int Round { get; set; }                             // próxima fecha (índice del fixture)
    public List<DtTableRow> Table { get; set; } = new();
    public string Formation { get; set; } = "4-3-3";
    public List<string> Lineup { get; set; } = new();          // ids de tus 11 titulares elegidos

    // Estilo de juego / táctica (sistema táctico del PDF)
    public string Mentality { get; set; } = "equilibrada";     // ultradef | def | equilibrada | of | ultraof
    public string Tempo { get; set; } = "normal";              // lento | normal | rapido
    public string Press { get; set; } = "media";               // baja | media | alta
    public string Build { get; set; } = "posesion";            // posesion | directo | contra
    public string Line { get; set; } = "media";                // alta | media | baja
    public bool SeasonInPlay { get; set; }                     // hay una temporada en curso (fixture sin terminar)

    // Calendario por meses/semanas (pedido de Alexander)
    public List<DtWeek> Calendar { get; set; } = new();
    public int Week { get; set; }                              // semana actual del calendario
    public string TrainPlan { get; set; } = "tactica";         // plan de entrenamiento elegido
    public string TrainIntensity { get; set; } = "normal";     // baja | normal | alta
    public string WeekMsg { get; set; } = "";                  // qué pasó en la última semana
    public DtDecision? PressPending { get; set; }              // entrevista de prensa que frena el avance

    // ---- Acumulados de carrera ----
    public int Titles { get; set; }
    public int Matches { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public int SeasonsManaged { get; set; }
    public int TimesFired { get; set; }
    public List<string> ClubsManaged { get; set; } = new();
    public List<DtSeason> Timeline { get; set; } = new();

    public string FullName => $"{Name} {Surname}".Trim();
}
