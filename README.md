# lsg-sdk-dotnet

Repositorio único para el **runtime .NET/C#** del ecosistema de mods LSG.
Agrupa el SDK-core reusable y los adaptadores de cada juego que comparta
este runtime (BepInEx, SMAPI, tModLoader, API de mods de Cities: Skylines).

Ver decisión de topología multi-repo (por runtime, no por cluster técnico)
en la conversación de diseño del `20260702` — resumen:

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
│   └── LSG.SDK.Core/        ← Auth, Api, Mechanics, Offline, Models (agnóstico de juego)
└── adapters/
    └── CoreKeeper.LSG.Mod/  ← primer adaptador (BepInEx), en construcción
```

Cada adaptador nuevo se agrega como `adapters/<Juego>.LSG.Mod/` y referencia
`LSG.SDK.Core` vía `ProjectReference` mientras el SDK está en desarrollo
activo, migrando a `PackageReference` (NuGet privado) cuando exista un tag
estable — así un tesista puede fijar versión de su adaptador sin arrastrar
cambios en curso de otro juego.

## Estado de adaptadores

| Adaptador | Cluster | Estado |
|---|---|---|
| `Raft.LSG.Mod` | BEPINEX | v1.0.0-rc1 — login interactivo (HUD OnGUI) + saldo + canje real de Paddle Speed Boost, verificado end-to-end en juego. `Loot Luck Boost` sigue placeholder (falta ubicar en dnSpy el sistema real de loot). Pendiente: confirmar que `OnGUI()` sí se ejecuta (mismo riesgo que `Update()`/`Start()`, no confirmado aún en juego real) |
| `Valheim.LSG.Mod` | BEPINEX | No iniciado |
| `Subnautica.LSG.Mod` | BEPINEX | No iniciado |
| `VRising.LSG.Mod` | BEPINEX | No iniciado |
| `StardewValley.LSG.Mod` | SMAPI | No iniciado |
| `Terraria.LSG.Mod` | TMODLOADER | No iniciado |
| ~~`CoreKeeper.LSG.Mod`~~ | ~~BEPINEX~~ | **Archivado** (`adapters/_archived/`) — descartado por infactibilidad de modding, ver `ARCHIVED.md`. Reemplazado por Raft. |

> Garry's Mod (reemplazo de Starbound) NO pertenece a este repo — es
> `LUA_SCRIPT`, corresponde a `lsg-sdk-lua` (repo aún no creado).

Ver `src/LSG.SDK.Core/README.md` para el contrato de mecánicas mínimas
cargadas y el diseño de `IEffectInterpreter`.
