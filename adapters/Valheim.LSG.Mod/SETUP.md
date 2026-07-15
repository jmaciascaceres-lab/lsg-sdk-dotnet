# Setup - Valheim.LSG.Mod

Versión: v0.2.0 (2026-07-15)

## Checklist de gotchas ya resueltos en Raft (no deberían repetirse aquí)

Este scaffold ya incorpora las lecciones de `Raft.LSG.Mod` - si algo de esto vuelve a fallar, es la EXCEPCIÓN, no la regla:

- OK: `Newtonsoft.Json` en vez de `System.Text.Json` (el SDK-core ya migró)
- OK: `ILRepack.targets` como archivo aparte (no target inline en el `.csproj`)
- OK: `CopyLocalLockFileAssemblies=true` en el `.csproj`
- OK: `UnityEngine.InputLegacyModule` + `UnityEngine.IMGUIModule` referenciados desde el inicio
- OK: `RedeemMechanicAsync` genérico en `Plugin.cs` (no hace falta un método por mecánica)

## Pendiente de confirmar (específico de Valheim, no heredado de Raft)

### 1. Mono vs IL2CPP

Valheim históricamente corre en **Mono** con BepInEx 5.4.x (mismo que Raft), pero **hay que confirmarlo** igual que hicimos con Raft: abrir `assembly_valheim.dll` (o el ensamblado principal del juego - confirmar nombre exacto, puede variar) directo en dnSpy. Si abre sin pedir "unhollow"/generar interop, es Mono - este `.csproj` sirve tal cual. Si dnSpy no puede decompilarlo normalmente, es IL2CPP y hay que migrar a BepInEx 6.x
(mismo ajuste que hubiéramos necesitado para Core Keeper).

### 2. Ruta de instalación / nombre de la carpeta Managed

```
Versión BepInEx: misma rama que Raft (5.4.x Mono x64), a confirmar tras (1)
Ruta de referencia: copiar valheim.local.props.example -> valheim.local.props y completar VALHEIM_MANAGED_PATH / BEPINEX_PATH reales
```

### 3. Puntos de enganche reales (dnSpy)

Necesito, mismo criterio que con `Paddle` en Raft - no adivinar nombres:

| Mecánica (mmv) | Qué buscar en dnSpy |
|---|---|
| **Stamina Regen Boost (60)** | Clase `Player`, buscar `staminaRegen`, `GetStaminaRegen`, `m_staminaRegen` - necesito el método/campo real que determina la regeneración por segundo, y si es accesible sin reflexión |
| **Comfort Boost (61)** | Buscar `Comfort`, `SEMan`, `ComfortLevel` - Valheim calcula "comfort" dinámicamente en base a objetos cercanos (fogatas, sillas, decoraciones), así que puede no ser un valor simple que sumar - necesito ver la clase completa antes de diseñar el patch |

Con las capturas armamos `ValheimEffectInterpreter` real, mismo patrón que `RaftEffectInterpreter` (Harmony patch + `*State` estático + revert vía `TimedEffectTracker`).

## Estado actual (2026-07-10)

`ValheimEffectInterpreter` implementado con las dos mecánicas reales, confirmadas en dnSpy sobre `assembly_valheim.dll`:

| Mecánica | Mecanismo | Harmony |
|---|---|---|
| Stamina Regen Boost (mmv=60) | `SE_Stats` creado en runtime + `SEMan.AddStatusEffect` (sistema nativo de Valheim) | **No requiere** |
| Comfort Boost (mmv=61) | Harmony `Postfix` sobre `SE_Rested.CalculateComfortLevel(bool, Vector3)` | Sí (mismo patrón que `PaddleForcePatch` en Raft) |

**Validado end-to-end en juego real** (login, catálogo, canje de ambas mecánicas, efecto confirmado con logs objetivos de `ValheimEffectInterpreter`/`ComfortBoostPatch`).

**HUD reposicionado (v0.2.0):** el default de Raft (`HudX=10, HudY=10`) solapaba las ranuras 1-3 del hotbar de Valheim. Default acá: `HudY=160` (config `LSG HUD` en el `.cfg` generado por BepInEx) para despejarlo. **Confirmado visualmente en juego** - sin solapamiento.

## Referencias

- R. González-Ibáñez, J. I. Macías-Cáceres and M. V. Paucar, "LifeSync-Games: A Technical Note on a Novel Framework for Video Game Development," 2025 44th International Conference of the Chilean Computer Science Society (SCCC), Valparaiso, Chile, 2025, pp. 1-4, doi: 10.1109/SCCC67219.2025.11420722.
- González-Ibáñez R., Macías-Cáceres J., Villalta-Paucar M. (2025). LifeSync-Games: Toward a Video Game Paradigm for Promoting Responsible Gaming and Human Development. arXiv:2510.19691 [cs.HC]. DOI: https://arxiv.org/abs/2510.19691