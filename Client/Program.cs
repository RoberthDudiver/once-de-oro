using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using OnceDeOro;
using OnceDeOro.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddSingleton<MatchEngine>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<MultiplayerService>();
builder.Services.AddScoped<Loc>();
builder.Services.AddScoped<Sfx>();
 builder.Services.AddScoped<StatsService>();

await builder.Build().RunAsync();
