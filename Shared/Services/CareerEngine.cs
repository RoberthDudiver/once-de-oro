using OnceDeOro.Data;
using OnceDeOro.Models;

namespace OnceDeOro.Services;

/// <summary>
/// El simulador de carrera: crea un jugador de 16 años y lo hace vivir su
/// trayectoria temporada a temporada. En cada cruce de caminos ofrece TRES
/// decisiones (cantera, préstamo, transferencia, quedarse, retirarse) y de ahí
/// depende cómo evoluciona el OVR, el valor, los títulos y la Selección.
/// </summary>
public static class CareerEngine
{
    private static readonly Random Rng = new();

    // OVR mínimo para ser titular fijo en cada nivel de club.
    private static int StarterMin(int tier) => tier switch { 1 => 46, 2 => 56, 3 => 66, 4 => 75, 5 => 82, _ => 46 };

    // Nivel de club que "merece" un jugador según su OVR.
    private static int Level(int ovr) => ovr >= 84 ? 5 : ovr >= 76 ? 4 : ovr >= 66 ? 3 : ovr >= 56 ? 2 : 1;

    // ---- Arranque ----
    public static CareerPlayer NewCareer(string surname, int number, Foot foot, string nationName, Position pos, int decisionEvery, string kit)
    {
        var nat = CareerData.NationByName(nationName);
        var cp = new CareerPlayer
        {
            Surname = string.IsNullOrWhiteSpace(surname) ? "Crack" : surname.Trim(),
            Number = Math.Clamp(number, 1, 99),
            Foot = foot,
            Nation = nat.Name, NationCode = nat.Code, NationTier = nat.Tier,
            Pos = pos,
            Kit = string.IsNullOrWhiteSpace(kit) ? "#e23b3b" : kit,
            DecisionEvery = Math.Clamp(decisionEvery, 1, 3),
        };
        cp.Pending = FirstOffer(cp);
        return cp;
    }

    private static CareerDecision FirstOffer(CareerPlayer cp)
    {
        var d = new CareerDecision
        {
            Title = "Oferta de cantera",
            Text = "Tres clubes quieren sumarte a su proyecto juvenil. Elegí dónde empieza tu carrera.",
        };
        var top = CareerData.PickClubs(2, 1, "", Rng);   // uno de segunda
        var low = CareerData.PickClubs(1, 2, "", Rng);   // dos de ascenso
        foreach (var c in top.Concat(low))
        {
            int tier = top.Contains(c) ? 2 : 1;
            d.Options.Add(new CareerOption { Label = $"Fichar por {c}", Sub = CareerData.League(tier, Rng), Club = c, Tier = tier, Kind = "transfer" });
        }
        return d;
    }

    // ---- Avanzar tras elegir una opción ----
    public static void Choose(CareerPlayer cp, CareerOption opt)
    {
        if (cp.Retired) return;

        if (opt.Kind == "retire") { Retire(cp); return; }

        // Aplicar el destino (fichaje/préstamo/quedarse).
        if (opt.Kind != "stay" && !string.IsNullOrEmpty(opt.Club))
        {
            cp.Club = opt.Club;
            cp.Tier = opt.Tier;
        }
        cp.Started = true;

        // Simular las temporadas hasta la próxima decisión.
        int seasons = cp.DecisionEvery;
        for (int i = 0; i < seasons && cp.Age < 39; i++)
            PlaySeason(cp);

        // Balón de Oro: si fue una bestia en un club top.
        if (cp.Tier >= 5 && cp.Ovr >= 90 && Rng.NextDouble() < 0.30)
        {
            cp.Ballons++;
            if (cp.Timeline.Count > 0) cp.Timeline[^1].Note = "🥇 Balón de Oro";
        }

        cp.Pending = cp.Age >= 39 || (cp.Age >= 34 && cp.Ovr < 60) ? RetireOffer(cp) : NextDecision(cp);
    }

    // ---- Una temporada ----
    private static void PlaySeason(CareerPlayer cp)
    {
        int tier = Math.Max(1, cp.Tier);
        bool starter = cp.Ovr >= StarterMin(tier) - 3;
        int apps = starter ? Rng.Next(26, 39) : Rng.Next(6, 20);

        double gpa = cp.Pos switch { Position.FWD => 0.55, Position.MID => 0.24, Position.DEF => 0.06, Position.GK => 0.0, _ => 0.2 };
        double form = Math.Clamp(cp.Ovr / 80.0, 0.4, 1.4);
        int goals = (int)Math.Round(apps * gpa * form * (0.7 + Rng.NextDouble() * 0.7));
        int assists = (int)Math.Round(apps * (gpa * 0.5 + 0.05) * form * (0.6 + Rng.NextDouble() * 0.7));

        // Altibajos: la carrera no es una línea recta. Un año podés romperla, otro
        // caer en un bajón o pasarlo entre lesiones.
        string formNote = "";
        bool injury = false;
        double dice = Rng.NextDouble();
        if (dice < 0.09)   // año con lesiones
        {
            injury = true;
            apps = (int)(apps * 0.45); goals = (int)(goals * 0.4); assists = (int)(assists * 0.4);
            formNote = "🤕 Año con lesiones";
        }
        else if (dice < 0.20) // bajón de forma
        {
            goals = (int)(goals * 0.6); assists = (int)(assists * 0.7);
            formNote = "📉 Bajón de forma";
        }
        else if (dice > 0.90) // temporada consagratoria
        {
            goals = (int)(goals * 1.4); assists = (int)(assists * 1.25);
            formNote = "🔥 Temporada consagratoria";
        }

        bool performed = (goals + assists) >= apps * 0.35 || (cp.Pos == Position.GK && starter);

        int growth = Growth(cp.Age, tier, performed) - (injury ? 1 : 0);
        cp.Ovr = Math.Clamp(cp.Ovr + growth, 40, 99);
        cp.PeakOvr = Math.Max(cp.PeakOvr, cp.Ovr);
        cp.Apps += apps; cp.Goals += goals; cp.Assists += assists;
        cp.ValueK = Value(cp.Ovr, cp.Age);

        string note = "";
        void Win(string t) { cp.Trophies.Add(t); note = "Campeón · " + t; }

        // Títulos de club: tiradas INDEPENDIENTES (podés ganar liga y copa el mismo
        // año). Cuanto mejor el club, más chances. Una carrera de elite deja vitrina.
        if (tier == 5)
        {
            if (Rng.NextDouble() < 0.55) Win("🏆 Liga");
            if (Rng.NextDouble() < 0.45) Win("🏅 Copa");
            if (Rng.NextDouble() < 0.35) Win("⭐ Champions");
        }
        else if (tier == 4)
        {
            if (Rng.NextDouble() < 0.42) Win("🏆 Liga");
            if (Rng.NextDouble() < 0.30) Win("🏅 Copa");
        }
        else if (tier == 3)
        {
            if (Rng.NextDouble() < 0.22) Win("🏆 Liga");
            if (Rng.NextDouble() < 0.15) Win("🏅 Copa");
        }

        // Selección.
        if (!cp.InNationalTeam && cp.Ovr >= 77)
        { cp.InNationalTeam = true; if (note == "") note = "🎽 Debut en la Selección"; }
        if (cp.InNationalTeam)
        {
            int caps = Rng.Next(5, 11);
            cp.Caps += caps;
            if (cp.Pos == Position.FWD || cp.Pos == Position.MID)
                cp.NationalGoals += (int)Math.Round(caps * gpa * form);
            // Título con la Selección (más chance si sos de una potencia y sos figura).
            double natChance = cp.NationTier switch { 1 => 0.16, 2 => 0.09, 3 => 0.045, _ => 0.02 };
            if (cp.Ovr >= 82 && Rng.NextDouble() < natChance)
            { cp.Trophies.Add($"🌍 Título con {cp.NationCode}"); note = $"🌍 Campeón con {cp.Nation}"; }
        }

        if (note == "") note = formNote;   // si no hubo título, mostrar el altibajo

        cp.Timeline.Add(new CareerYear
        {
            Age = cp.Age, Club = cp.Club, Tier = tier, Ovr = cp.Ovr,
            Apps = apps, Goals = goals, Assists = assists, Note = note,
        });
        cp.Age++;
    }

    private static int Growth(int age, int tier, bool performed)
    {
        int g = age switch
        {
            <= 18 => Rng.Next(3, 7),
            <= 21 => Rng.Next(2, 5),
            <= 25 => Rng.Next(1, 4),
            <= 28 => Rng.Next(0, 2),
            <= 31 => -Rng.Next(0, 2),
            <= 34 => -Rng.Next(1, 3),
            _ => -Rng.Next(2, 4),
        };
        if (tier >= 4 && age <= 25) g += 1;   // mejor entorno de entrenamiento
        if (performed && age <= 30) g += 1;
        return g;
    }

    private static int Value(int ovr, int age)
    {
        double b = Math.Pow(Math.Max(0, ovr - 40) / 10.0, 3.3) * 900;   // en miles de €
        double af = age <= 20 ? 0.65 + (age - 16) * 0.09
                  : age <= 27 ? 1.15
                  : Math.Max(0.12, 1.15 - (age - 27) * 0.14);
        return (int)Math.Max(50, b * af);
    }

    // ---- Decisiones ----
    private static CareerDecision NextDecision(CareerPlayer cp)
    {
        int lvl = Level(cp.Ovr);
        bool veteran = cp.Age >= 32;
        var d = new CareerDecision();

        if (cp.Age <= 19)
        {
            d.Title = "Salida a préstamo";
            d.Text = "Tu club quiere que sumes minutos. Elegí dónde seguir tu desarrollo.";
            AddClubOptions(d, cp, Math.Max(2, lvl), 3, loan: true);
        }
        else if (cp.Age <= 24)
        {
            d.Title = "Te empiezan a mirar";
            d.Text = "Tu nombre suena. Podés dar el salto o afianzarte donde estás.";
            AddClubOptions(d, cp, Math.Min(5, Math.Max(cp.Tier + 1, lvl)), 2, loan: false);
            d.Options.Add(Stay(cp));
        }
        else if (!veteran)
        {
            d.Title = lvl >= 5 ? "Interés de la elite" : "Ventana de transferencias";
            d.Text = "Estás en tu mejor momento. Hay ofertas sobre la mesa.";
            AddClubOptions(d, cp, Math.Min(5, Math.Max(cp.Tier, lvl)), 2, loan: false);
            d.Options.Add(Stay(cp));
        }
        else
        {
            d.Title = "Recta final";
            d.Text = "Los años pesan. Podés pelear arriba, buscar minutos o pensar en el retiro.";
            AddClubOptions(d, cp, Math.Max(1, Math.Min(cp.Tier, lvl)), 1, loan: false);
            d.Options.Add(Stay(cp));
            d.Options.Add(new CareerOption { Label = "Retirarte", Sub = "colgar los botines", Kind = "retire" });
        }
        return d;
    }

    private static void AddClubOptions(CareerDecision d, CareerPlayer cp, int tier, int count, bool loan)
    {
        foreach (var c in CareerData.PickClubs(tier, count, cp.Club, Rng))
            d.Options.Add(new CareerOption
            {
                Label = (loan ? "Préstamo en " : "Fichar por ") + c,
                Sub = CareerData.League(tier, Rng),
                Club = c, Tier = tier, Kind = loan ? "loan" : "transfer",
            });
    }

    private static CareerOption Stay(CareerPlayer cp) => new()
    {
        Label = $"Quedarte en {cp.Club}",
        Sub = "renovar y seguir",
        Club = cp.Club, Tier = cp.Tier, Kind = "stay",
    };

    private static CareerDecision RetireOffer(CareerPlayer cp) => new()
    {
        Title = "Final del camino",
        Text = "El cuerpo dijo basta. Es momento de cerrar una gran carrera.",
        Options = { new CareerOption { Label = "Retirarte", Sub = "colgar los botines", Kind = "retire" } },
    };

    private static void Retire(CareerPlayer cp)
    {
        cp.Retired = true;
        cp.Pending = null;
    }

    /// <summary>Etiqueta de leyenda final según lo logrado.</summary>
    public static string Legacy(CareerPlayer cp)
    {
        int titles = cp.Trophies.Count;
        bool mundial = cp.Trophies.Any(t => t.StartsWith("🌍"));
        if (cp.PeakOvr >= 92 && titles >= 8 && (mundial || cp.Ballons > 0)) return "🐐 Leyenda del fútbol";
        if (cp.PeakOvr >= 87 && titles >= 6) return "👑 Ídolo mundial";
        if (cp.PeakOvr >= 80 && titles >= 3) return "⭐ Crack";
        if (cp.PeakOvr >= 73) return "🎯 Gran figura";
        if (cp.PeakOvr >= 64) return "✅ Profesional sólido";
        return "🌱 Carrera digna";
    }
}
