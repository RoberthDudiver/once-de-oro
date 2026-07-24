namespace OnceDeOro.Models;

/// <summary>Nivel de un logro, al estilo de los trofeos de PlayStation.</summary>
public enum AchievementTier { Bronce, Plata, Oro, Platino }

/// <summary>Un logro desbloqueable. La lógica de cuándo se gana vive en GameService.</summary>
public sealed class Achievement
{
    public required string Id { get; init; }
    public required string Emoji { get; init; }
    public required string Name { get; init; }
    public required string Desc { get; init; }
    public AchievementTier Tier { get; init; }
}
