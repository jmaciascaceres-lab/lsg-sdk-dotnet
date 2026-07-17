# LSG .NET SDK (lsg-sdk-dotnet)

Versión: v1.1.1 (2026-07-15)

Repositorio único para el **runtime .NET/C#** del ecosistema de mods LSG. Agrupa el SDK-core reusable y los adaptadores de cada juego que comparta este runtime (BepInEx, SMAPI, tModLoader, API de mods de Cities: Skylines).

Ver decisión de topología multi-repo (por runtime, no por cluster técnico):

| Repo | Runtime | Clusters/juegos |
|---|---|---|
| **`lsg-sdk-dotnet`** (este) | .NET/C#, NuGet | BEPINEX, SMAPI, TMODLOADER, CITIES_MODAPI |
| `lsg-sdk-lua` (futuro) | Lua embebido | Factorio, Total War, Cyberpunk 2077, Starbound, Project Zomboid |
| `lsg-sdk-cpp` (futuro) | C++/CMake | Ark, HumanitZ, Satisfactory |
| `lsg-sdk-java` (futuro) | Java/Gradle | Minecraft |

## Estructura

```
lsg-sdk-dotnet/
├── LSG.SDK.sln
├── src/
│   └── LSG.SDK.Core/ ← Auth, Api, Mechanics, Offline, Models (agnóstico de juego)
└── adapters/
    ├── Raft.LSG.Mod/ ← cerrado, v1.1.1 (plantilla de referencia)
    ├── Valheim.LSG.Mod/ ← validado end-to-end, v0.2.0
    └── _archived/
        └── CoreKeeper.LSG.Mod/ ← descartado, ver ARCHIVED.md
```

Cada adaptador nuevo se agrega como `adapters/<Juego>.LSG.Mod/` y referencia `LSG.SDK.Core` vía `ProjectReference` mientras el SDK está en desarrollo activo, migrando a `PackageReference` (NuGet privado) cuando exista un tag estable.

## Patrón consolidado para nuevos adaptadores BEPINEX

Raft y Valheim ya resolvieron (y documentaron) los mismos gotchas de entorno. Cualquier adaptador BEPINEX nuevo debería heredarlos desde el scaffold inicial, no redescubrirlos:

- **`Newtonsoft.Json`**, no `System.Text.Json` (incompatible con Mono viejo - ver `src/LSG.SDK.Core/README.md`).
- **`ILRepack.targets`** como archivo aparte (no target inline en el `.csproj`), fusiona solo `LSG.SDK.Core.dll` + `Newtonsoft.Json.dll`.
- **`BepInEx/config/BepInEx.cfg` → `[Preloader] HideManagerGameObject = true`** desde el inicio - evita que `Update()`/`Start()`/`OnGUI()` nunca lleguen a ejecutarse (causa: el juego destruye el GameObject administrador antes del primer frame).
- **HUD (`OnGUI`) con posición configurable** (`LSG HUD > HudX/HudY` en el `.cfg`) - cada juego tiene su propia distribución de UI nativa; no asumir que la esquina superior izquierda está libre.
- Antes de escribir cualquier patch de Harmony, **revisar primero si el juego ya expone un sistema oficial de buffs/efectos** (ej. `SEMan`/`StatusEffect` en Valheim) - puede evitar Harmony por completo para algunas mecánicas.
- Verificar Mono vs IL2CPP con dnSpy antes de armar el `.csproj` (abre directo = Mono; pide "unhollow" = IL2CPP, requiere BepInEx 6.x).

## Estado de adaptadores

| Adaptador | Cluster | Estado |
|---|---|---|
| `Raft.LSG.Mod` | BEPINEX | **v1.1.1 - CERRADO.** Login interactivo, HUD (posición configurable), saldo, ambas mecánicas (Paddle Speed Boost + Loot Luck Boost, redefinido como garantía de ítem) validadas con efecto real en juego, verificado con logs objetivos. |
| `Valheim.LSG.Mod` | BEPINEX | **v0.2.0 - validado end-to-end en juego real.** Stamina Regen Boost vía `SEMan` nativo (sin Harmony), Comfort Boost vía Harmony `Postfix`, ambos confirmados con logs objetivos. HUD reposicionado para no solapar el hotbar. |
| `Subnautica.LSG.Mod` | BEPINEX | No iniciado |
| `VRising.LSG.Mod` | BEPINEX (**IL2CPP**, no Mono) | Scaffold compilable (`BasePlugin`, .NET 6). Diseño de ambas mecánicas confirmado sin Harmony. **Pendiente bloqueante:** resolución de `Entity` del jugador (`VWorld.TryGetTargetPlayerEntity`) — ver `SETUP.md` |
| `StardewValley.LSG.Mod` | SMAPI | No iniciado |
| `Terraria.LSG.Mod` | TMODLOADER | No iniciado |
| ~~`CoreKeeper.LSG.Mod`~~ | ~~BEPINEX~~ | **Archivado** (`adapters/_archived/`) - descartado por infactibilidad de modding, ver `ARCHIVED.md`. Reemplazado por Raft. |

> Garry's Mod (reemplazo de Starbound) NO pertenece a este repo - es `LUA_SCRIPT`, corresponde a `lsg-sdk-lua` (repo aún no creado).

Ver `src/LSG.SDK.Core/README.md` para el contrato de mecánicas mínimas cargadas, el diseño de `IEffectInterpreter`, y el historial de incompatibilidades resueltas (Mono/System.Text.Json).

## Changelog

### v1.1.1 (2026-07-15)

- Adaptador de Valheim completado:
  - Login interactivo y HUD con balance en tiempo real.
  - Mecánicas implementadas: Stamina Regen Boost y Comfort Boost.
  - Catch-up automático de eventos offline.

### v1.1.0 (2026-07-10)

- Adaptador de Raft completado:
  - Login interactivo y HUD con balance en tiempo real.
  - Mecánicas implementadas: Paddle Speed Boost y Loot Luck Boost (garantía de ítem).
  - Catch-up automático de eventos offline.
- Refactor de LSG SDK Core: migración a Newtonsoft.Json (build limpio).

## Referencias

- R. González-Ibáñez, J. I. Macías-Cáceres and M. V. Paucar, "LifeSync-Games: A Technical Note on a Novel Framework for Video Game Development," 2025 44th International Conference of the Chilean Computer Science Society (SCCC), Valparaiso, Chile, 2025, pp. 1-4, doi: 10.1109/SCCC67219.2025.11420722.
- González-Ibáñez R., Macías-Cáceres J., Villalta-Paucar M. (2025). LifeSync-Games: Toward a Video Game Paradigm for Promoting Responsible Gaming and Human Development. arXiv:2510.19691 [cs.HC]. DOI: https://arxiv.org/abs/2510.19691
