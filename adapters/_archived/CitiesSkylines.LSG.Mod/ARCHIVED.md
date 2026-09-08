# CitiesSkylines.LSG.Mod (Cities: Skylines original, 2015) — ARCHIVADO (2026-08-25)

## Motivo

Bloqueo arquitectónico probablemente fatal, no un simple bug: el juego está
efectivamente limitado a **.NET Framework 3.5** (confirmado en la
documentación oficial de modding: *"any language version that can be
compiled to target .NET Framework 3.5 will do"*), un runtime **anterior a
la existencia de `HttpClient`/`System.Net.Http`** (introducidos recién en
.NET 4.5, 2012). Todo `LSG.SDK.Core` está construido sobre `HttpClient`.

Se reemplaza el juego por **Cities: Skylines II** (`game_id=73` en LSG,
distinto de este `game_id=14`) — runtime mucho más moderno (.NET Standard
2.1 Mono), sin este problema.

## Diagnóstico completo (por si se retoma)

- Compilaba sin errores (`netstandard2.1`), pero el juego rechazaba el
  `.dll` al cargarlo: `"Failed to load the mod's dll file, or one of its
  dependencies"`.
- Se descartó la hipótesis de `netstandard.dll` faltante como fix simple
  (confirmado: no existe en `Cities_Data\Managed\`, y aunque existiera,
  el problema de fondo es más profundo).
- Señal indirecta fuerte: el mod real de multijugador para este juego
  (`CSM - Cities Skylines Multiplayer`) evita HTTP por completo y usa
  `LiteNetLib` (sockets UDP de bajo nivel) — coherente con que HTTP normal
  no sea viable en este runtime.

## Camino NO explorado, si algún día se retoma

Reimplementar la capa de red de `LSG.SDK.Core` sobre `System.Net.Sockets`
crudo (`TcpClient`/`NetworkStream`) en vez de `HttpClient`, armando el
protocolo HTTP a mano. **Riesgo real sin confirmar antes de invertir ese
trabajo:** LSG-Core-API corre sobre HTTPS, y el soporte de TLS en un Mono
tan viejo (2015) es un problema frecuente en ese ecosistema — muchos de esos
runtimes ni siquiera soportan TLS 1.2, el mínimo que exigen casi todos los
servidores HTTPS modernos. Si ese fuera el caso, todo el trabajo de
reimplementar el cliente HTTP sería en vano — vale la pena confirmar el
soporte de TLS 1.2 **antes** de empezar esa reimplementación, no después.

## Qué SÍ quedó resuelto (reutilizable si se retoma)

- Diseño de ambas mecánicas confirmado contra la API oficial `ICities`
  (`EconomyExtensionBase.OnUpdateMoneyAmount` para Cash Income Bonus,
  `IMilestones.UnlockMilestone` para Milestone Unlock Boost — mismo
  mecanismo del cheat nativo "Unlock All" del juego).
- Catálogo real completo en LSG (`game_id=14`, mmv=5 y mmv=214).
- Patrón de registro por reflexión (`IUserMod`, `ThreadingExtensionBase`,
  etc.) documentado en `SETUP.md`.

Ver `SETUP.md` en esta misma carpeta para el detalle técnico completo.
