using OnceDeOro.Server;
using OnceDeOro.Server.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Carga los static web assets del cliente (index.html + _framework) al correr con
// `dotnet run`. En la imagen publicada no hay manifiesto y esto es un no-op inofensivo:
// ahí los archivos ya están físicamente en wwwroot y los sirve UseBlazorFrameworkFiles.
builder.WebHost.UseStaticWebAssets();

builder.Services.AddSignalR();
builder.Services.AddSingleton<RoomManager>();

var app = builder.Build();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRouting();

app.MapHub<DuelHub>("/duelhub");
app.MapFallbackToFile("index.html");

app.Run();
