# Setup — Terraria.LSG.Mod

## Confirmado

- `game_id = 8` (LSG-Core-API, cluster `TMODLOADER`).
- **tModLoader es 100% open source en GitHub** (`tModLoader/tModLoader`) — a
  diferencia de todos los demás adaptadores, no necesitamos dnSpy en
  absoluto. Todo el diseño de este adaptador se confirmó leyendo el código
  fuente/documentación oficial directamente.

## Diferencia estructural importante: dónde vive el proyecto

A diferencia de Raft/Valheim/VRising/StardewValley (compilables directo
desde este repo), el `.csproj` de tModLoader importa `..\tModLoader.targets`
con ruta relativa a la instalación del juego — el proyecto **debe vivir
físicamente** en:
```
Documents\My Games\Terraria\tModLoader\ModSources\LSGTerrariaMod\
```

**Flujo de trabajo:** los archivos `.cs` de este repo (`adapters/Terraria.LSG.Mod/`)
son un respaldo versionado — hay que copiarlos a mano a la carpeta real de
`ModSources\LSGTerrariaMod\` para compilar. Para la referencia a
`LSG.SDK.Core`, la forma más simple y robusta es copiar la carpeta
`src\LSG.SDK.Core\` completa **dentro** de `ModSources\LSGTerrariaMod\` (como
subcarpeta local), en vez de depender de una ruta relativa larga entre dos
carpetas de `Documentos` distintas.

## Hallazgo clave: casi todo el catálogo se resuelve con UN mecanismo genérico

Del catálogo real (211 registros para `game_id=8`), **~180 tienen `reward_id`
en `options`** (mascotas, monturas, pociones nativas de Terraria) — estos
`reward_id` son los IDs nativos de `BuffID` del juego. Confirmado en el
código fuente oficial (`ExamplePet.cs`, `ExampleHood.cs`):

```csharp
player.AddBuff(int buffType, int time, bool quiet = true);
```

**Un solo intérprete genérico cubre las ~180 mecánicas de una sola vez** —
no hace falta implementarlas una por una. El catálogo real, además, ya trae
`cost_amount`/`attribute_id` en `options` para todas ellas, así que el ciclo
de canje los lee directo, sin necesitar config por mecánica.

Las ~30 mecánicas restantes (mmv 7-33 aprox., sin `reward_id`, con
descripciones reales tipo "Aumenta la velocidad de ataque...") son
**custom** — necesitan código específico por mecánica, vía hooks de
`ModPlayer`/`ModSystem`.

## Mecánicas implementadas (v0.1) — COMPILADO Y VALIDADO EN JUEGO REAL

| Alcance | Mecanismo |
|---|---|
| **~180 mecánicas con `reward_id`** | `Player.AddBuff(reward_id, durationTicks)` — genérico |
| **Player Movement Speed (mmv=16)** | `ModPlayer.PostUpdateRunSpeeds()`, multiplicando `maxRunSpeed`/`runAcceleration` |

Ninguna de las dos necesita Harmony.

### Player Movement Speed — diseño confirmado en el código fuente

`ModPlayer.cs` (oficial, GitHub):
```csharp
/// This is called after the player's horizontal speeds are modified...
/// Use this to modify maxRunSpeed, accRunSpeed, runAcceleration...
public virtual void PostUpdateRunSpeeds() { }
```
Mismo patrón que Raft/Valheim ("estado propio + hook por tick"), pero con el
hook nativo de tModLoader en vez de Harmony. `LSGPlayer.LsgSpeedBuffActive`
se pone en `true` al aplicar, y `Revert()` lo apaga — a diferencia de la vía
genérica (`AddBuff`), que expira sola sin necesitar `Revert()`.

### Hooks de `ModSystem` confirmados (documentación oficial)

- `OnModLoad()` — "called right after Mod.Load()" — login/catálogo acá.
- `PostUpdateEverything()` — "the last hook that happens in an update" —
  `TimedEffectTracker.Tick()`/flush de cola offline acá.

### Pieza NO re-verificada contra fuente esta sesión

`ModCommand` (comando de chat `/lsgredeem`) — API estable y muy usada en la
comunidad, pero a diferencia de las 4 piezas de arriba, no se confirmó
línea por línea contra el código fuente. Si algo no compila, es el primer
lugar a revisar.

## Instalación

1. Copiar los `.cs` de `adapters/Terraria.LSG.Mod/` (este repo) a
   `Documents\My Games\Terraria\tModLoader\ModSources\LSGTerrariaMod\`
   (NO copiar el `.csproj`/`build.txt` de este repo — esos ya existen ahí,
   generados por tModLoader; solo agregar la línea `<ProjectReference
   Include="LSG.SDK.Core\LSG.SDK.Core.csproj" />` dentro del `ItemGroup` del
   `.csproj` real).
2. Copiar `src\LSG.SDK.Core\` (este repo) completa dentro de
   `ModSources\LSGTerrariaMod\LSG.SDK.Core\`.
3. Editar `LSGModSystem.cs` y reemplazar `LsgEmail`/`LsgPassword` (hardcodeado
   en el código por ahora — ver sección de pendientes).
4. Compilar: desde tModLoader, **Workshop → Develop Mods → Build + Reload**
   (o `dotnet build` parado en la carpeta `ModSources\LSGTerrariaMod\`).
5. Con una partida cargada, en el chat del juego: `/lsgredeem 16` (Player
   Movement Speed) o `/lsgredeem 90` (Araña Buff, por ejemplo).

## Gotcha confirmado por el compilador (no por la documentación)

El código fuente de GitHub (rama consultada) usa `player`/`mod` (minúscula) como
propiedades heredadas de `ModPlayer`/`ModSystem`. **La versión instalada real
(1.4.4.9+2026.06.3.6) usa `Player`/`Mod` (mayúscula)** — el compilador de esa
versión rechazó `player`/`mod` minúscula con `CS0103`. Puede deberse a que la
rama de GitHub fetcheada correspondía a una versión ligeramente distinta a la
instalada. **Regla general: si el código de este adaptador no compila por un
nombre de propiedad/campo, probar la otra capitalización antes que nada.**

## Pendiente — no bloqueante

- Credenciales hardcodeadas en el código en vez de un archivo de config —
  tModLoader tiene su propio `Terraria.ModLoader.Config.ModConfig` para
  menús en el juego, evitado a propósito por el choque de nombres con
  nuestras clases y para no sumar otra API nueva en el primer intento.
- Extender las ~30 mecánicas custom restantes (Faster Attack Speed,
  Increased Player Max HP, Reduced Enemy HP, etc.) — cada una necesita su
  propio hook de `ModPlayer` (probablemente `ModifyWeaponDamage`,
  `ModifyMaxStats` o similar — sin confirmar todavía).
- Sin compilar/probar en juego todavía.
