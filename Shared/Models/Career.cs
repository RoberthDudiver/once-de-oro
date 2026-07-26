namespace OnceDeOro.Models;

/// <summary>Pierna hábil del jugador de carrera.</summary>
public enum Foot { Izquierda, Derecha }

/// <summary>Una temporada registrada en la trayectoria (una fila de la línea de tiempo).</summary>
public sealed class CareerYear
{
    public int Age { get; set; }
    public string Club { get; set; } = "";
    public int Tier { get; set; }
    public int Ovr { get; set; }
    public int Apps { get; set; }
    public int Goals { get; set; }
    public int Assists { get; set; }
    /// <summary>Hito de la temporada: título, debut en la Selección, etc. (vacío si nada especial).</summary>
    public string Note { get; set; } = "";
}

/// <summary>Una de las tres opciones de una decisión de carrera.</summary>
public sealed class CareerOption
{
    public string Label { get; set; } = "";   // "Fichar por River", "Quedarte", "Retirarte"
    public string Sub { get; set; } = "";      // liga / contexto
    public string Club { get; set; } = "";     // destino (vacío si continuar/retiro)
    public int Tier { get; set; }              // nivel del club destino
    public string Kind { get; set; } = "transfer"; // transfer | loan | stay | retire
}

/// <summary>El cruce de caminos: título + relato + tres opciones.</summary>
public sealed class CareerDecision
{
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public List<CareerOption> Options { get; set; } = new();
}

/// <summary>
/// Un jugador de CARRERA (distinto del DT/avatar): se crea, vive una trayectoria
/// año a año tomando decisiones y se retira con un palmarés. Se guarda aparte.
/// </summary>
public sealed class CareerPlayer
{
    // ---- Identidad ----
    public string Surname { get; set; } = "";
    public int Number { get; set; } = 10;
    public Foot Foot { get; set; } = Foot.Derecha;
    public string Nation { get; set; } = "Argentina";
    public string NationCode { get; set; } = "ARG";
    public int NationTier { get; set; } = 1;    // 1 = potencia mundial … 4 = modesta
    public Position Pos { get; set; } = Position.FWD;
    public string Kit { get; set; } = "#e23b3b"; // color de la camiseta
    public int DecisionEvery { get; set; } = 2; // dificultad: temporadas entre decisiones

    // ---- Progreso ----
    public int Age { get; set; } = 16;
    public int Ovr { get; set; } = 50;
    public int ValueK { get; set; } = 100;      // valor de mercado en miles de €
    public string Club { get; set; } = "";
    public int Tier { get; set; }
    public bool Started { get; set; }           // ya eligió su primer club
    public bool Retired { get; set; }
    public bool InNationalTeam { get; set; }

    // ---- Totales de carrera ----
    public int Apps { get; set; }
    public int Goals { get; set; }
    public int Assists { get; set; }
    public int Caps { get; set; }
    public int NationalGoals { get; set; }
    public int PeakOvr { get; set; } = 50;
    public int Ballons { get; set; }            // balones de oro ganados
    public List<string> Trophies { get; set; } = new();
    public List<CareerYear> Timeline { get; set; } = new();

    /// <summary>La decisión que está esperando al jugador ahora (null si se retiró).</summary>
    public CareerDecision? Pending { get; set; }

    /// <summary>Ya se sumó como leyenda al club del modo manager (para no duplicar).</summary>
    public bool AddedToClub { get; set; }

    public string ShortPos => Pos switch
    {
        Position.GK => "POR", Position.DEF => "DFC", Position.MID => "MC", _ => "DC"
    };
}
