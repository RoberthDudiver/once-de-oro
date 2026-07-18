# ⚽ Once de Oro — Manager de Fútbol

Juego web tipo **manager de fútbol** con un giro propio: **armás tu equipo desde cero con un presupuesto, comprás y vendés jugadores en el mercado, entrás a torneos, cobrás premios y reinvertís** para construir un equipo cada vez más fuerte.

Estética **broadcast premium** (TV deportiva): dorados, glassmorphism, tipografía condensada, marcador animado y feed de goles en vivo.

## 🎮 El loop de juego

1. **Mercado** — Empezás con `$200M`. Fichás leyendas de los Mundiales (1950-2026) y agentes libres baratos. Los cracks cuestan una fortuna; vendés al 90% del valor.
2. **Equipo** — Elegís formación (4-3-3, 4-4-2, 3-5-2, 5-3-2, 4-2-3-1) y estilo (ofensivo / equilibrado / defensivo), y armás tu XI en el campo.
3. **Torneos** — Cuatro competiciones de prestigio creciente: **Copa Regional → Copa América → Champions League → Copa del Mundo**. Cada una tiene inscripción, rivales temáticos y premios.
4. **Partido** — Antes de jugar ves el **pronóstico** (modelos **Poisson + Elo**: % de victoria/empate/derrota, goles esperados, marcadores más probables). Después mirás el partido en una **cancha 2D animada**: 22 jugadores y la pelota moviéndose jugada a jugada, con faltas, tarjetas, atajadas, pausas de hidratación, entretiempo, **prórroga y penales** en las eliminatorias, más estadísticas en vivo (posesión, remates, faltas).
5. **Progresión** — Ganás premios, reforzás el plantel y escalás hasta el Mundial. **Meta legendaria:** ser campeón invicto sin recibir un solo gol.

## 🌐 Jugar contra amigos (online)

Modo **Duelo online** con salas por código:

1. Un jugador **crea una sala** y elige el tipo de juego:
   - **Equipos:** Draft parejo (arman su XI con un presupuesto compartido) o Equipo de carrera.
   - **Formato:** Partido único, Mejor de 3, o Mini-torneo (3-4 jugadores, bracket).
2. Comparte el **código de 4 letras**; los amigos se unen.
3. Cada uno arma/confirma su equipo; el **servidor simula** los partidos y los transmite **en vivo y sincronizados** a todos (marcador, goleadores reales, penales).

La simulación es **autoritativa en el servidor** (SignalR) para evitar trampas y mantener a todos en sincronía.

## 🧱 Arquitectura

Solución de 3 proyectos (`.NET 10`):

| Proyecto | Contenido |
|---|---|
| `Shared/` | `Models` (`Player`, `Formation`, `Competition`, timeline, DTOs de multiplayer), `Data` (roster + torneos), `MatchEngine` (motor rápido, incl. PvP) y `MatchSimulator` (motor por eventos + predicción Poisson/Elo) |
| `Client/` | Blazor WebAssembly: `Pages` (Home, Mercado, Equipo, Torneos, Partido, **Online**), `Services` (`GameService`, `MultiplayerService`), `Components` (incl. `MatchField` — la cancha 2D animada) |
| `Server/` | ASP.NET Core: sirve el WASM + `DuelHub` (SignalR) + `RoomManager` (salas y contienda en vivo) |

- **Carrera (single-player):** persiste en `localStorage` (clave `once-de-oro:save:v1`), sin backend.
- **Online:** requiere el `Server` corriendo (SignalR en `/duelhub`).

## ▶️ Correr en local

```bash
dotnet run --project Server/OnceDeOro.Server.csproj --urls http://localhost:5235
# http://localhost:5235  (sirve el juego + el hub online)
```

## 🚀 Publicar

```bash
dotnet publish Server/OnceDeOro.Server.csproj -c Release
# App hosteada (WASM + SignalR). Desplegar como los demás servicios .NET del ecosistema.
```

---
*Parte del ecosistema Dudiver.*
