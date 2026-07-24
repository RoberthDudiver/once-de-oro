using System.Security.Cryptography;
using MongoDB.Driver;
using OnceDeOro.Models;

namespace OnceDeOro.Server.Accounts;

/// <summary>
/// Persistencia en MongoDB de cuentas y partidas guardadas.
/// La cadena de conexión llega por configuración (`MongoDB__ConnectionString`);
/// NUNCA va en el repo porque es público. Si no está configurada, <see cref="Enabled"/>
/// es false y la app funciona igual: el juego sigue guardando en localStorage.
/// </summary>
public sealed class AccountStore
{
    private readonly IMongoCollection<UserAccount>? _users;
    private readonly IMongoCollection<SaveDocument>? _saves;
    private readonly IMongoCollection<VisitorDoc>? _visitors;

    public bool Enabled { get; }

    public AccountStore(IConfiguration config, ILogger<AccountStore> log)
    {
        var conn = config["MongoDB:ConnectionString"];
        var dbName = config["MongoDB:Database"] ?? "oncedeoro";

        if (string.IsNullOrWhiteSpace(conn))
        {
            Enabled = false;
            log.LogWarning("MongoDB no configurado: las cuentas quedan deshabilitadas y el juego sólo guarda en el navegador.");
            return;
        }

        var db = new MongoClient(conn).GetDatabase(dbName);
        _users = db.GetCollection<UserAccount>("users");
        _saves = db.GetCollection<SaveDocument>("saves");
        _visitors = db.GetCollection<VisitorDoc>("visitors");

        // Un email = una cuenta.
        _users.Indexes.CreateOne(new CreateIndexModel<UserAccount>(
            Builders<UserAccount>.IndexKeys.Ascending(u => u.Email),
            new CreateIndexOptions { Unique = true }));

        Enabled = true;
        log.LogInformation("MongoDB conectado (base '{Db}') para cuentas y partidas.", dbName);
    }

    public static string Normalize(string email) => (email ?? "").Trim().ToLowerInvariant();

    public Task<UserAccount?> FindByEmailAsync(string email) =>
        _users!.Find(u => u.Email == Normalize(email)).FirstOrDefaultAsync()!;

    public Task<UserAccount?> FindByIdAsync(string id) =>
        _users!.Find(u => u.Id == id).FirstOrDefaultAsync()!;

    public async Task InsertAsync(UserAccount user) => await _users!.InsertOneAsync(user);

    public async Task TouchLoginAsync(string userId) =>
        await _users!.UpdateOneAsync(u => u.Id == userId,
            Builders<UserAccount>.Update.Set(u => u.LastLoginAt, DateTime.UtcNow));

    public Task<SaveDocument?> GetSaveAsync(string userId) =>
        _saves!.Find(s => s.UserId == userId).FirstOrDefaultAsync()!;

    public async Task UpsertSaveAsync(string userId, GameState state)
    {
        var doc = new SaveDocument
        {
            UserId = userId,
            State = state,
            UpdatedAt = DateTime.UtcNow,
            Progress = ProgressScore(state),
        };
        await _saves!.ReplaceOneAsync(s => s.UserId == userId, doc, new ReplaceOptions { IsUpsert = true });
    }

    /// <summary>
    /// Cuánto avanzó una partida. Se usa para no pisar nunca un progreso mayor
    /// con uno menor cuando el mismo usuario entra desde otro dispositivo.
    /// </summary>
    public static int ProgressScore(GameState s) =>
        s.MatchesPlayed * 10 + s.Honours.Count * 100 + s.History.Count * 20 + s.OwnedIds.Count;

    // ---- Administración ----

    /// <summary>Todos los usuarios con los datos clave de su partida, para el panel de admin.</summary>
    public async Task<List<AdminUserRow>> AdminUsersAsync(IEnumerable<string> rootEmails)
    {
        if (_users is null || _saves is null) return new();
        var roots = rootEmails.ToHashSet();

        var users = await _users.Find(_ => true).SortByDescending(u => u.LastLoginAt).ToListAsync();
        var saves = await _saves.Find(_ => true).ToListAsync();
        var byId = saves.ToDictionary(s => s.UserId);

        return users.Select(u =>
        {
            byId.TryGetValue(u.Id, out var sd);
            var st = sd?.State;
            bool isRoot = roots.Contains(u.Email);
            return new AdminUserRow(
                u.Id, u.Email, u.DisplayName, u.CreatedAt, u.LastLoginAt, u.Banned,
                st?.ClubName ?? "—", st?.Money ?? 0, st?.MatchesPlayed ?? 0, st?.Wins ?? 0,
                st?.Honours.Count ?? 0, sd?.Progress ?? 0,
                u.IsAdmin || isRoot, isRoot);
        }).ToList();
    }

    /// <summary>Nombra (o quita) admin a una cuenta desde el panel.</summary>
    public async Task SetAdminAsync(string userId, bool isAdmin)
    {
        if (_users is null) return;
        await _users.UpdateOneAsync(u => u.Id == userId,
            Builders<UserAccount>.Update.Set(u => u.IsAdmin, isAdmin));
    }

    /// <summary>¿La cuenta tiene el flag de admin en la base? (los emails raíz se chequean aparte).</summary>
    public async Task<bool> IsAccountAdminAsync(string userId)
    {
        if (_users is null) return false;
        var u = await _users.Find(x => x.Id == userId).FirstOrDefaultAsync();
        return u?.IsAdmin ?? false;
    }

    /// <summary>Suma (o resta) dinero a la partida de un usuario. Devuelve el nuevo total, o null si no hay partida.</summary>
    public async Task<int?> AdjustMoneyAsync(string userId, int delta)
    {
        if (_saves is null) return null;
        var save = await _saves.Find(s => s.UserId == userId).FirstOrDefaultAsync();
        if (save is null) return null;
        save.State.Money = Math.Max(0, save.State.Money + delta);
        save.UpdatedAt = DateTime.UtcNow;
        await _saves.ReplaceOneAsync(s => s.UserId == userId, save);
        return save.State.Money;
    }

    /// <summary>Fija el dinero exacto de la partida de un usuario. Devuelve el nuevo total, o null si no hay partida.</summary>
    public async Task<int?> SetMoneyAsync(string userId, int money)
    {
        if (_saves is null) return null;
        var save = await _saves.Find(s => s.UserId == userId).FirstOrDefaultAsync();
        if (save is null) return null;
        save.State.Money = Math.Max(0, money);
        save.UpdatedAt = DateTime.UtcNow;
        await _saves.ReplaceOneAsync(s => s.UserId == userId, save);
        return save.State.Money;
    }

    /// <summary>Suspende o rehabilita una cuenta.</summary>
    public async Task SetBannedAsync(string userId, bool banned)
    {
        if (_users is null) return;
        await _users.UpdateOneAsync(u => u.Id == userId,
            Builders<UserAccount>.Update.Set(u => u.Banned, banned));
    }

    public async Task<bool> IsBannedAsync(string userId)
    {
        if (_users is null) return false;
        var u = await _users.Find(x => x.Id == userId).FirstOrDefaultAsync();
        return u?.Banned ?? false;
    }

    // ---- Contraseñas: PBKDF2-SHA256, 210k iteraciones ----
    private const int Iterations = 210_000;

    public static (string hash, string salt) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public static bool VerifyPassword(string password, string hash, string salt)
    {
        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(salt)) return false;
        var expected = Convert.FromBase64String(hash);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(salt), Iterations, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    // ---- Contador anónimo de jugadores ----
    // Guardamos SOLO un UUID que genera el navegador, cuándo se lo vio, cuántos
    // partidos jugó y el idioma. Nada personal: no hay email, ni IP, ni nombre.

    /// <summary>Registra un ping del navegador y actualiza sus totales.</summary>
    public async Task PingAsync(string visitorId, long matches, string lang)
    {
        if (_visitors is null || string.IsNullOrWhiteSpace(visitorId)) return;
        if (visitorId.Length > 40) visitorId = visitorId[..40];
        lang = lang is "es" or "en" or "pt" ? lang : "es";
        var now = DateTime.UtcNow;

        // matches del cliente NUNCA baja: es el máximo visto (por si borra el save).
        await _visitors.UpdateOneAsync(
            v => v.Id == visitorId,
            Builders<VisitorDoc>.Update
                .SetOnInsert(v => v.FirstSeen, now)
                .Set(v => v.LastSeen, now)
                .Set(v => v.Lang, lang)
                .Max(v => v.Matches, matches),
            new UpdateOptions { IsUpsert = true });
    }

    /// <summary>Los totales para el apartado de análisis.</summary>
    public async Task<StatsResponse> StatsAsync()
    {
        if (_visitors is null || _users is null)
            return new StatsResponse(0, 0, 0, 0, new());

        long players = await _visitors.EstimatedDocumentCountAsync();
        long accounts = await _users.EstimatedDocumentCountAsync();

        var desde = DateTime.UtcNow.AddDays(-7);
        long activos = await _visitors.CountDocumentsAsync(v => v.LastSeen >= desde);

        // suma de partidos y reparto por idioma, en una sola pasada de agregación
        var group = await _visitors.Aggregate()
            .Group(v => v.Lang, g => new { Lang = g.Key, Matches = g.Sum(x => x.Matches) })
            .ToListAsync();

        long totalMatches = group.Sum(g => g.Matches);
        var porIdioma = group.ToDictionary(g => g.Lang, g => g.Matches);

        return new StatsResponse(players, totalMatches, accounts, activos, porIdioma);
    }
}
