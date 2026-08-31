# Setup — StardewValley.LSG.Mod

## Confirmado

- `game_id = 12` (LSG-Core-API, cluster `SMAPI`).
- Runtime: SMAPI — mucho más simple que Raft/Valheim/VRising: .NET normal,
  un solo proceso, sin capa IL2CPP, `StardewValley.dll` se abre directo en
  dnSpy sin pasos intermedios (a diferencia de VRising/Il2CppDumper).

## Catálogo real de mecánicas (game_id=12)

13 mecánicas activas + 1 registro de prueba de saldo insuficiente
(`mmv=89`, "SV_EXPENSIVE", excluido a propósito) + 1 registro suelto
aparentemente de una iteración anterior (`mmv=6`, "Faster Peasants",
`options` con esquema distinto y descripción "placeholder" - excluido).

**Implementadas y VALIDADAS EN JUEGO REAL (2026-08-25):**

| mmv | Nombre | Tipo | Mecanismo (confirmado en dnSpy) |
|---|---|---|---|
| 77 | Speed Buff | buff | `Farmer.applyBuff(new Buff(..., effects: new BuffEffects { Speed = X }, duration: Yms))` |
| 84 | Mining XP | modifier | `Farmer.gainExperience(3, amount)` (`3` = índice de Mining, confirmado por el switch interno del método) |

**Pendientes de implementar (mismo patrón, extensión directa):** Foraging
Buff (mmv=79), Fishing Buff (mmv=80), Farming Buff (mmv=81), Luck Buff
(mmv=82, ojo: `gainExperience` bloquea explícitamente `which==5`/Luck, hay
que investigar una vía alternativa), Stamina Buff (mmv=83), Foraging XP
(mmv=85), Fishing XP (mmv=86), Combat XP (mmv=87), Farming XP (mmv=88).

**Dato importante:** el catálogo real **no trae `duration_seconds` ni
magnitud** en `options` para ninguna mecánica (a diferencia de
Raft/Valheim/VRising) — solo `reward_id`/`cost_amount`/`attribute_id`. La
duración/magnitud de cada efecto vive en `ModConfig` (`config.json`), no en
el catálogo de LSG.

## Diseño confirmado en dnSpy

### Speed Buff (mmv=77)

`Farmer.addedSpeed` tiene un setter marcado `[Obsolete]` que **no hace nada**
(`set { }`) — el propio mensaje de obsolescencia indica el camino correcto:

```csharp
[Obsolete("Player speed can't be changed directly. You can add a speed buff via applyBuff instead (and optionally mark it invisible).")]
```

Camino real:
```csharp
var effects = new BuffEffects();
effects.Speed.Value = speedAmount;
var buff = new Buff(id: "lsg_speed_buff", duration: durationMs, effects: effects, ...);
Game1.player.applyBuff(buff);
```

El propio `Buff` maneja su expiración (`millisecondsDuration`) — no hace
falta `Revert()` manual ni trackearlo en `TimedEffectTracker` (mismo patrón
que Movement Speed Boost en VRising vía `LifeTime`).

### Mining XP (mmv=84)

```csharp
public virtual void gainExperience(int which, int howMuch)
```

`which` confirmado por el switch interno del método: `0`=Farming, `1`=Fishing,
`2`=Foraging, `3`=Mining, `4`=Combat, `5`=Luck (**bloqueado explícitamente**
al inicio del método — `if (which == 5 ...) return;` — Luck no se puede
otorgar por esta vía, dato relevante para cuando se implemente Luck Buff).

Dispara el flujo real del juego (subida de nivel, mensajes) — no es un simple
contador.

**Confirmado en juego real:** `lsg_speed` → ícono de buff visible + velocidad
real notoria (`ledger_id=2898`). `lsg_mining` → confirmado con múltiples
cargas acumuladas (`ledger_id=2899` a `2905`, `experiencePoints[3]` de `59` a
`398`, +339 en total) — la barra de experiencia de Mining subió de nivel
visiblemente en el juego, cerrando cualquier duda sobre el efecto real.

## Instalación

1. Instalar SMAPI: https://smapi.io/ (instalador oficial, detecta la
   instalación del juego solo).
2. `dotnet build adapters\StardewValley.LSG.Mod\StardewValley.LSG.Mod.csproj -c Release`
   — `Pathoschild.Stardew.ModBuildConfig` copia el mod compilado a
   `Stardew Valley\Mods\StardewValley.LSG.Mod\` automáticamente, sin paso de
   "copy" manual.
3. Completar `Email`/`Password` en
   `Stardew Valley\Mods\StardewValley.LSG.Mod\config.json` (se genera solo la
   primera vez que corre el mod).
4. Iniciar el juego vía `StardewModdingAPI.exe` (no el `.exe` normal).
5. Con una partida cargada, en la consola de SMAPI: `lsg_speed` o `lsg_mining`.

## Pendiente — no bloqueante

- Extender a las 9 mecánicas restantes del catálogo (mismo patrón: buffs vía
  `applyBuff`, XP vía `gainExperience` con el índice de skill correspondiente).
- HUD/UI en juego — SMAPI tiene su propia API de dibujo
  (`IModHelper.Events.Display.RenderedHud`); por ahora los comandos de
  consola alcanzan para probar.
- `Revert()` queda vacío a propósito (ninguna de las dos mecánicas
  implementadas lo necesita todavía).
