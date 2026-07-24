using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using OnceDeOro.Models;

namespace OnceDeOro.Server.Accounts;

/// <summary>
/// Cuenta de usuario. Hoy sólo registro local (email + contraseña); el campo
/// <see cref="Provider"/> deja lugar para Google más adelante sin migrar nada.
/// </summary>
public sealed class UserAccount
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Email normalizado (minúsculas y sin espacios). Índice único.</summary>
    public string Email { get; set; } = "";

    public string DisplayName { get; set; } = "";

    /// <summary>PBKDF2-SHA256. Vacío si la cuenta es de un proveedor externo.</summary>
    public string PasswordHash { get; set; } = "";
    public string PasswordSalt { get; set; } = "";

    /// <summary>"local" hoy; "google" cuando agreguemos OAuth.</summary>
    public string Provider { get; set; } = "local";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;

    /// <summary>Cuenta suspendida por un administrador: no puede iniciar sesión ni guardar en la nube.</summary>
    public bool Banned { get; set; }
}

/// <summary>
/// La partida guardada de un usuario. Guardamos el <see cref="GameState"/> completo
/// (equipo, dinero, torneo en curso, historial y estadísticas) para que no se pierda nada.
/// </summary>
public sealed class SaveDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string UserId { get; set; } = "";

    public GameState State { get; set; } = new();

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Sirve para elegir el save más avanzado cuando hay conflicto.</summary>
    public int Progress { get; set; }
}

/// <summary>
/// Un jugador ANONIMO, para el contador global. El "Id" es un UUID aleatorio que
/// genera el navegador la primera vez: no tiene nada que ver con vos, ni email ni
/// nombre. Sólo sirve para no contar dos veces al mismo navegador.
/// </summary>
public sealed class VisitorDoc
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = "";

    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    /// <summary>Cuántos partidos jugó este navegador (lo cuenta el propio cliente).</summary>
    public long Matches { get; set; }
    /// <summary>Idioma que tenía elegido, para el reparto por idioma. Nada más.</summary>
    public string Lang { get; set; } = "es";
}

// ---- Contratos de la API (lo que viaja entre cliente y servidor) ----

public sealed record RegisterRequest(string Email, string Password, string? DisplayName);
public sealed record LoginRequest(string Email, string Password);
public sealed record AuthResponse(string Token, string Email, string DisplayName);

/// <summary>Lo que manda el navegador al arrancar: su UUID anónimo y sus totales.</summary>
public sealed record PingRequest(string VisitorId, long Matches, string Lang);

/// <summary>Los números públicos del contador.</summary>
public sealed record StatsResponse(long Players, long Matches, long Accounts,
                                   long Active7d, Dictionary<string, long> ByLang);

// ---- Administración (sólo para el/los admin configurados) ----

/// <summary>Una fila de la tabla de usuarios del panel de administrador.</summary>
public sealed record AdminUserRow(
    string Id, string Email, string DisplayName, DateTime CreatedAt, DateTime LastLoginAt,
    bool Banned, string ClubName, int Money, int MatchesPlayed, int Wins, int Honours, int Progress);

public sealed record AdjustMoneyRequest(int Delta);
public sealed record SetMoneyRequest(int Money);
public sealed record BanRequest(bool Banned);
