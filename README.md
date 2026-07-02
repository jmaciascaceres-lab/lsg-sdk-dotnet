# LSG-SDK-Dotnet

Repositorio único para el **runtime .NET/C#** del ecosistema de mods LSG.

Agrupa el SDK-core reusable y los adaptadores de cada juego que comparta este runtime (BepInEx, SMAPI, tModLoader, API de mods de Cities: Skylines).

Decision de topologia multi-repo (por runtime, no por cluster técnico):

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

Cada adaptador nuevo se agrega como `adapters/<Juego>.LSG.Mod/` y referencia `LSG.SDK.Core` vía `ProjectReference` mientras el SDK está en desarrollo activo, migrando a `PackageReference` (NuGet privado) cuando exista un tag estable.

## Estado de adaptadores

| Adaptador | Cluster | Estado |
|---|---|---|
| `CoreKeeper.LSG.Mod` | BEPINEX | Scaffold - pendiente `IEffectInterpreter`, login interactivo, HUD |
| `Valheim.LSG.Mod` | BEPINEX | No iniciado |
| `Subnautica.LSG.Mod` | BEPINEX | No iniciado |
| `VRising.LSG.Mod` | BEPINEX | No iniciado |
| `StardewValley.LSG.Mod` | SMAPI | No iniciado |
| `Terraria.LSG.Mod` | TMODLOADER | No iniciado |

Ver `src/LSG.SDK.Core/README.md` para el contrato de mecánicas mínimas cargadas y el diseño de `IEffectInterpreter`.

## BepInEx

- `Core Keeper` -> https://thunderstore.io/c/core-keeper/p/BepInEx/BepInExPack_Core_Keeper/

## Changelog

### v0.1.0 (2026-07-03)

**Features:**
- Core SDK scaffold.
- Primer adaptador: `CoreKeeper.LSG.Mod`.
- Login, cache de mecánicas, canje.

---

## Referencias

- R. González-Ibáñez, J. I. Macías-Cáceres and M. V. Paucar, "LifeSync-Games: A Technical Note on a Novel Framework for Video Game Development," 2025 44th International Conference of the Chilean Computer Science Society (SCCC), Valparaiso, Chile, 2025, pp. 1-4, doi: 10.1109/SCCC67219.2025.11420722.
- González-Ibáñez R., Macías-Cáceres J., Villalta-Paucar M. (2025). *LifeSync-Games: Toward a Video Game Paradigm for Promoting Responsible Gaming and Human Development*. arXiv:2510.19691 [cs.HC]. DOI: https://arxiv.org/abs/2510.19691
