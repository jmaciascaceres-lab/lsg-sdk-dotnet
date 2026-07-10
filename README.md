# LSG .NET SDK (lsg-sdk-dotnet)

Versión: v1.1.0 (2026-07-10)

Repositorio único para el **runtime .NET/C#** del ecosistema de mods LSG. Agrupa el SDK-core reusable y los adaptadores de cada juego que comparta
este runtime (BepInEx, SMAPI, tModLoader, API de mods de Cities: Skylines).

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
    └── CoreKeeper.LSG.Mod/ ← primer adaptador (BepInEx), en construcción
```

Cada adaptador nuevo se agrega como `adapters/<Juego>.LSG.Mod/` y referencia `LSG.SDK.Core` vía `ProjectReference` mientras el SDK está en desarrollo activo, migrando a `PackageReference` (NuGet privado) cuando exista un tag estable - así un tesista puede fijar versión de su adaptador sin arrastrar cambios en curso de otro juego.

## Estado de adaptadores

| Adaptador | Cluster | Estado |
|---|---|---|
| `Raft.LSG.Mod` | BEPINEX | **v1.1.0 - CERRADO.** Login interactivo, HUD, saldo, ambas mecánicas (Paddle Speed Boost + Loot Luck Boost, redefinido como garantía de ítem) validadas con efecto real en juego. Pendiente opcional: estilizado del HUD |
| `Valheim.LSG.Mod` | BEPINEX | No iniciado |
| `Subnautica.LSG.Mod` | BEPINEX | No iniciado |
| `VRising.LSG.Mod` | BEPINEX | No iniciado |
| `StardewValley.LSG.Mod` | SMAPI | No iniciado |
| `Terraria.LSG.Mod` | TMODLOADER | No iniciado |
| ~~`CoreKeeper.LSG.Mod`~~ | ~~BEPINEX~~ | **Archivado** (`adapters/_archived/`) - descartado por infactibilidad de modding, ver `ARCHIVED.md`. Reemplazado por Raft. |

> Garry's Mod (reemplazo de Starbound) NO pertenece a este repo - es `LUA_SCRIPT`, corresponde a `lsg-sdk-lua` (repo aún no creado).

Ver `src/LSG.SDK.Core/README.md` para el contrato de mecánicas mínimas cargadas y el diseño de `IEffectInterpreter`.

## Changelog

### v1.1.0 (2026-07-10)

- Adaptador de Raft completado:
  - Login interactivo y HUD con balance en tiempo real.
  - Mecánicas implementadas: Paddle Speed Boost y Loot Luck Boost (garantía de ítem).
  - Catch-up automático de eventos offline.
- Refactor de LSG SDK Core: migración a Newtonsoft.Json (build limpio).

## Referencias

- R. González-Ibáñez, J. I. Macías-Cáceres and M. V. Paucar, "LifeSync-Games: A Technical Note on a Novel Framework for Video Game Development," 2025 44th International Conference of the Chilean Computer Science Society (SCCC), Valparaiso, Chile, 2025, pp. 1-4, doi: 10.1109/SCCC67219.2025.11420722.
- González-Ibáñez R., Macías-Cáceres J., Villalta-Paucar M. (2025). LifeSync-Games: Toward a Video Game Paradigm for Promoting Responsible Gaming and Human Development. arXiv:2510.19691 [cs.HC]. DOI: https://arxiv.org/abs/2510.19691
