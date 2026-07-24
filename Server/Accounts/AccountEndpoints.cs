using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using OnceDeOro.Models;

namespace OnceDeOro.Server.Accounts;

public static class AccountEndpoints
{
    public static string JwtSecret(IConfiguration cfg) =>
        cfg["Auth:JwtSecret"] is { Length: >= 32 } s
            ? s
            : "once-de-oro-desarrollo-local-secreto-de-32+chars";

    private const string Issuer = "once-de-oro";

    /// <summary>
    /// Emails con permiso de administrador. Se configuran por entorno
    /// (Admin__Emails="a@x.com;b@y.com"); si no, sólo el dueño del juego.
    /// El email NO es un secreto, así que puede vivir en el repo público.
    /// </summary>
    private static string[] AdminEmails(IConfiguration cfg)
    {
        var raw = cfg["Admin:Emails"];
        if (string.IsNullOrWhiteSpace(raw)) return new[] { "rdudiver@gmail.com" };
        return raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .Select(e => e.ToLowerInvariant()).ToArray();
    }

    /// <summary>Admin RAÍZ: el email está en Admin__Emails. No se puede quitar desde el panel.</summary>
    private static bool IsRootAdmin(ClaimsPrincipal me, IConfiguration cfg)
    {
        var email = (me.FindFirstValue(JwtRegisteredClaimNames.Email) ?? me.FindFirstValue(ClaimTypes.Email) ?? "").ToLowerInvariant();
        return !string.IsNullOrEmpty(email) && AdminEmails(cfg).Contains(email);
    }

    /// <summary>Admin = raíz (por email) O nombrado desde el panel (flag en la base).</summary>
    private static async Task<bool> IsAdminAsync(ClaimsPrincipal me, IConfiguration cfg, AccountStore store)
    {
        if (IsRootAdmin(me, cfg)) return true;
        var id = UserId(me);
        return id is not null && await store.IsAccountAdminAsync(id);
    }

    private static string? UserId(ClaimsPrincipal me) =>
        me.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? me.FindFirstValue(ClaimTypes.NameIdentifier);

    private static string CreateToken(UserAccount user, IConfiguration cfg)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret(cfg)));
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Issuer,
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("name", user.DisplayName),
            },
            expires: DateTime.UtcNow.AddDays(180),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static void MapAccountEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        // ¿Hay cuentas disponibles en este despliegue?
        api.MapGet("/auth/status", (AccountStore store) => Results.Ok(new { enabled = store.Enabled }));

        // Contador anónimo de jugadores. El ping no necesita cuenta ni token: es
        // sólo un UUID de navegador y sus totales, sin nada personal.
        api.MapPost("/stats/ping", async (PingRequest req, AccountStore store) =>
        {
            await store.PingAsync(req.VisitorId, req.Matches, req.Lang);
            return Results.Ok();
        });

        api.MapGet("/stats", async (AccountStore store) => Results.Ok(await store.StatsAsync()));

        api.MapPost("/auth/register", async (RegisterRequest req, AccountStore store, IConfiguration cfg) =>
        {
            if (!store.Enabled) return Results.Problem("Las cuentas no están habilitadas en este servidor.", statusCode: 503);

            var email = AccountStore.Normalize(req.Email);
            if (!email.Contains('@') || email.Length < 5)
                return Results.BadRequest(new { error = "Email inválido." });
            if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
                return Results.BadRequest(new { error = "La contraseña debe tener al menos 6 caracteres." });

            if (await store.FindByEmailAsync(email) is not null)
                return Results.Conflict(new { error = "Ya existe una cuenta con ese email." });

            var (hash, salt) = AccountStore.HashPassword(req.Password);
            var user = new UserAccount
            {
                Email = email,
                DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? email.Split('@')[0] : req.DisplayName!.Trim(),
                PasswordHash = hash,
                PasswordSalt = salt,
                Provider = "local",
            };
            await store.InsertAsync(user);

            return Results.Ok(new AuthResponse(CreateToken(user, cfg), user.Email, user.DisplayName));
        });

        api.MapPost("/auth/login", async (LoginRequest req, AccountStore store, IConfiguration cfg) =>
        {
            if (!store.Enabled) return Results.Problem("Las cuentas no están habilitadas en este servidor.", statusCode: 503);

            var user = await store.FindByEmailAsync(req.Email);
            if (user is null || !AccountStore.VerifyPassword(req.Password, user.PasswordHash, user.PasswordSalt))
                return Results.Json(new { error = "Email o contraseña incorrectos." }, statusCode: 401);

            if (user.Banned)
                return Results.Json(new { error = "Esta cuenta fue suspendida." }, statusCode: 403);

            await store.TouchLoginAsync(user.Id);
            return Results.Ok(new AuthResponse(CreateToken(user, cfg), user.Email, user.DisplayName));
        });

        // ---- Partida guardada ----
        var game = api.MapGroup("/game").RequireAuthorization();

        game.MapGet("/state", async (ClaimsPrincipal me, AccountStore store) =>
        {
            var id = me.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? me.FindFirstValue(ClaimTypes.NameIdentifier);
            if (id is null) return Results.Unauthorized();
            var save = await store.GetSaveAsync(id);
            return save is null ? Results.NoContent() : Results.Ok(save.State);
        });

        game.MapPut("/state", async (GameState state, ClaimsPrincipal me, AccountStore store) =>
        {
            var id = me.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? me.FindFirstValue(ClaimTypes.NameIdentifier);
            if (id is null) return Results.Unauthorized();
            if (await store.IsBannedAsync(id)) return Results.Json(new { error = "Cuenta suspendida." }, statusCode: 403);
            await store.UpsertSaveAsync(id, state);
            return Results.Ok(new { saved = true });
        });

        // ---- Administración (ver usuarios, tocar su dinero, banear) ----
        var admin = api.MapGroup("/admin").RequireAuthorization();

        // ¿El que pregunta es admin? Lo usa el cliente para mostrar (o no) el panel.
        admin.MapGet("/me", async (ClaimsPrincipal me, IConfiguration cfg, AccountStore store) =>
            Results.Ok(new { isAdmin = await IsAdminAsync(me, cfg, store) }));

        admin.MapGet("/users", async (ClaimsPrincipal me, IConfiguration cfg, AccountStore store) =>
        {
            if (!await IsAdminAsync(me, cfg, store)) return Results.Forbid();
            return Results.Ok(await store.AdminUsersAsync(AdminEmails(cfg)));
        });

        admin.MapPost("/users/{id}/money", async (string id, AdjustMoneyRequest req, ClaimsPrincipal me, IConfiguration cfg, AccountStore store) =>
        {
            if (!await IsAdminAsync(me, cfg, store)) return Results.Forbid();
            var money = await store.AdjustMoneyAsync(id, req.Delta);
            return money is null
                ? Results.NotFound(new { error = "Ese usuario todavía no tiene partida guardada." })
                : Results.Ok(new { money });
        });

        admin.MapPost("/users/{id}/set-money", async (string id, SetMoneyRequest req, ClaimsPrincipal me, IConfiguration cfg, AccountStore store) =>
        {
            if (!await IsAdminAsync(me, cfg, store)) return Results.Forbid();
            var money = await store.SetMoneyAsync(id, req.Money);
            return money is null
                ? Results.NotFound(new { error = "Ese usuario todavía no tiene partida guardada." })
                : Results.Ok(new { money });
        });

        admin.MapPost("/users/{id}/ban", async (string id, BanRequest req, ClaimsPrincipal me, IConfiguration cfg, AccountStore store) =>
        {
            if (!await IsAdminAsync(me, cfg, store)) return Results.Forbid();
            if (id == UserId(me)) return Results.BadRequest(new { error = "No podés banearte a vos mismo." });
            await store.SetBannedAsync(id, req.Banned);
            return Results.Ok(new { banned = req.Banned });
        });

        // Nombrar (o quitar) admin a otra cuenta. Sólo un admin puede; no podés
        // tocar tu propio estado ni el de un admin RAÍZ (los de Admin__Emails).
        admin.MapPost("/users/{id}/admin", async (string id, SetAdminRequest req, ClaimsPrincipal me, IConfiguration cfg, AccountStore store) =>
        {
            if (!await IsAdminAsync(me, cfg, store)) return Results.Forbid();
            if (id == UserId(me)) return Results.BadRequest(new { error = "No podés cambiar tu propio rol de admin." });

            var target = await store.FindByIdAsync(id);
            if (target is null) return Results.NotFound(new { error = "No existe ese usuario." });
            if (AdminEmails(cfg).Contains(target.Email))
                return Results.BadRequest(new { error = "Ese usuario es admin raíz (por email) y no se cambia desde acá." });

            await store.SetAdminAsync(id, req.IsAdmin);
            return Results.Ok(new { isAdmin = req.IsAdmin });
        });
    }
}
