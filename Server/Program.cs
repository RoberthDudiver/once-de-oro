using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OnceDeOro.Server;
using OnceDeOro.Server.Accounts;
using OnceDeOro.Server.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Carga los static web assets del cliente (index.html + _framework) al correr con
// `dotnet run`. En la imagen publicada no hay manifiesto y esto es un no-op inofensivo:
// ahí los archivos ya están físicamente en wwwroot y los sirve UseBlazorFrameworkFiles.
builder.WebHost.UseStaticWebAssets();

builder.Services.AddSignalR();
builder.Services.AddSingleton<RoomManager>();

// El estado del juego guarda enums (TeamStyle) que el cliente puede mandar como
// texto o como número: aceptamos ambos para que el save nunca se rechace.
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Cuentas + partidas guardadas en MongoDB. La cadena de conexión llega por
// configuración (MongoDB__ConnectionString); si falta, el juego sigue andando
// guardando sólo en el navegador.
builder.Services.AddSingleton<AccountStore>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "once-de-oro",
            ValidAudience = "once-de-oro",
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(AccountEndpoints.JwtSecret(builder.Configuration))),
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseBlazorFrameworkFiles();

// index.html y el manifest se revalidan SIEMPRE. Son los que apuntan a todo lo
// demas con su ?v=, asi que si el navegador se queda con una copia vieja de
// estos dos, sigue pidiendo el CSS y los iconos viejos por mas que se haya
// desplegado una version nueva. "no-cache" no es "no cachear": es "preguntá
// antes de usarlo", que es exactamente lo que hace falta.
var sinCache = new[] { ".html", ".webmanifest", "manifest.json" };
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var f = ctx.File.Name;
        if (sinCache.Any(x => f.EndsWith(x, StringComparison.OrdinalIgnoreCase)))
            ctx.Context.Response.Headers.CacheControl = "no-cache, must-revalidate";
    }
});
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapAccountEndpoints();
app.MapHub<DuelHub>("/duelhub");
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    OnPrepareResponse = ctx =>
        ctx.Context.Response.Headers.CacheControl = "no-cache, must-revalidate",
});

app.Run();
