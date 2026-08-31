# VRising.LSG.Mod — ARCHIVADO (2026-08-25)

## Motivo

Curva de integración desproporcionadamente alta frente al resto del
catálogo: IL2CPP (no Mono), servidor como proceso separado del cliente
(`VRisingServer.exe`), y — el bloqueante final — un crash nativo
(`AccessViolationException`) en `EntityManager.CreateEntityQuery` al
resolver la `Entity` del jugador, no resuelto tras varios intentos
(`ComponentType[]` → `ComponentType` suelto → `EntityQueryBuilder`, todos
con el mismo stack trace).

## No fue tiempo perdido

A diferencia de CoreKeeper (infactible desde el inicio), acá **sí llegamos
a tener el mod funcionando de punta a punta en juego real** — login,
catálogo, redeem contra LSG con saldo real, `World "Server"` encontrado
correctamente. El bloqueo fue específicamente en el último tramo (resolver
qué `Entity` de ECS corresponde al jugador para aplicar el efecto).

## Todo el diseño y la investigación quedan preservados

Ver `SETUP.md` en esta misma carpeta — incluye:
- El fix de `BepInExPack_V_Rising` (vs. BepInEx genérico) para el bug de
  Cpp2IL específico de VRising.
- El fix de `winhttp.dll` → `version.dll` para que doorstop se inyecte en
  el servidor headless.
- El diseño confirmado de ambas mecánicas (`DebugEventsSystem.ApplyBuff` +
  `Blood.Quality` directo) sin necesitar Harmony.
- La resolución de `Entity` del jugador vía `User.LocalCharacter`.
- El punto exacto donde quedó bloqueado, con el próximo paso a intentar si
  se retoma (`EntityQueryBuilder` u otra vía que evite el marshaling de
  arrays de `ComponentType` hacia IL2CPP).

Si en algún momento se retoma, empezar por ese `SETUP.md` — la mayor parte
del camino difícil ya está hecho.
