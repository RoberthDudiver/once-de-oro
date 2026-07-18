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

// ---- Contratos de la API (lo que viaja entre cliente y servidor) ----

public sealed record RegisterRequest(string Email, string Password, string? DisplayName);
public sealed record LoginRequest(string Email, string Password);
public sealed record AuthResponse(string Token, string Email, string DisplayName);
