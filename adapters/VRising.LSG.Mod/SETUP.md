# Setup — VRising.LSG.Mod

## Diferencia fundamental con Raft/Valheim: IL2CPP, no Mono

Confirmado en dnSpy (`GameAssembly.dll` presente, sin carpeta `MonoBleedingEdge`)
y en el log de arranque (`Runtime information: .NET 6.0.7`, BepInEx reporta
"IL2CPP"). Esto cambia toda la cadena de herramientas:

| | Raft / Valheim (Mono) | VRising (IL2CPP) |
|---|---|---|
| BepInEx | 5.4.x | **6.x** (rama IL2CPP) |
| Clase base del plugin | `BaseUnityPlugin` | `BasePlugin` |
| Punto de entrada | `Awake()` | `Load()` |
| TFM del `.csproj` | `netstandard2.1` | `net6.0` |
| Leer código en dnSpy | directo sobre `Assembly-CSharp.dll` | requiere **Il2CppDumper** primero (genera DLLs "dummy") |
| Referencias para **compilar** | las mismas DLLs del juego | `BepInEx/interop/*.dll` — generadas por BepInEx/Cpp2IL, **no** las de Il2CppDumper (esas solo sirven para dnSpy) |

## Historial de esta sesión (para no repetir la investigación)

### 1. Il2CppDumper — correr desde la carpeta del juego, no desde otra

Error `Win32Exception (126): No se puede encontrar el módulo especificado`
al cargar `GameAssembly.dll` — causa: el "custom PE loader" necesita que
Windows resuelva las dependencias de esa DLL (como `UnityPlayer.dll`) vía el
orden de búsqueda estándar, que incluye la carpeta desde la que se ejecuta el
`.exe`. **Fix:** copiar `Il2CppDumper.exe` (y sus archivos acompañantes)
directo a la carpeta de VRising y ejecutarlo desde ahí.

### 2. BepInEx 6.x genérico (GitHub) falla con Cpp2IL en VRising

Error `Failed to find Binary code or metadata registration` — bug conocido y
reportado múltiples veces contra BepInEx bleeding-edge genérico específicamente
para VRising (issues #866, #877, #879 del repo de BepInEx), causado porque
VRising actualizó su versión de Unity (2022.3.58f1) y la heurística de
detección automática de direcciones de Cpp2IL no la maneja bien en esa build
genérica.

**Fix: usar `BepInExPack_V_Rising` de Thunderstore, NO el BepInEx genérico de
GitHub** — es la distribución que la comunidad de modding de VRising empaqueta
y prueba contra cada actualización del juego:

```
https://v-rising.thunderstore.io/package/BepInEx/BepInExPack_V_Rising/
```

Con esta distribución, `Cpp2IL` sí encontró `codereg`/`metareg` y
`Il2CppInteropGen` generó la carpeta `BepInEx/interop/` completa (172
archivos, confirmado 2026-07-16).

### 3. Referencias del `.csproj` — repartidas en 3 carpetas distintas

- `BepInEx/core/` → `BepInEx.Unity.IL2CPP.dll`, `Il2CppInterop.Runtime.dll`, `0Harmony.dll` (runtime fijo de BepInEx)
- `BepInEx/interop/` → `ProjectM*.dll`, `Unity.Entities.dll`, etc. (generados por Cpp2IL para VRising específicamente)
- Il2CppDumper (carpeta aparte) → **solo para leer en dnSpy**, nunca para compilar

## Mecánicas: diseño confirmado, implementación con un pendiente

| Mecánica (mmv) | Mecanismo | Harmony |
|---|---|---|
| Movement Speed Boost (64) | `DebugEventsSystem.ApplyBuff` con `PrefabGUID` de un buff de velocidad existente (`-911970381`, "Voltatia's Electric Speed Buff" — reportado por la comunidad, no verificado en nuestro propio dump ya que es un dato de contenido, no código), luego sobreescribir `ModifyMovementSpeedBuff.MoveSpeed` en la Entity resultante | No |
| Blood Quality Insight (65) | Escritura directa de `Blood.Quality` (`IComponentData` simple) en la Entity del jugador | No |

Ninguna de las dos necesita Harmony — mejor que Raft y Valheim en ese sentido.
Patrón de `DebugEventsSystem`/`BuffUtility` adaptado de **KindredCommands**
(mod open-source, AGPL-3.0, `Buffs.cs`) — no copiado literal; los nombres de
`LifeTime`/extensiones `Has`/`Add`/`Remove` de esa referencia son helpers
propios de ese mod (`ECSExtensions.cs`), no del juego — acá se usa la API
estándar de `EntityManager` en su lugar.

## PENDIENTE — bloqueante para probar en juego real

**`VWorld.TryGetTargetPlayerEntity` no está implementado.** A diferencia de
Valheim (`Player.m_localPlayer`, campo estático directo), no se ha confirmado
en dnSpy una vía igual de simple para VRising. Antes de poder probar el ciclo
de canje real en juego, hay que investigar en dnSpy/`dump.cs`:

- Un componente tipo `PlayerCharacter`/`User` recorrible vía `EntityQuery`
  (más apropiado para un mod de servidor que podría aplicar a cualquier
  jugador conectado, no solo "el local").
- Confirmar si el flujo debe correr del lado del **servidor** (como
  `DebugEventsSystem`/`BuffUtility`, que viven en el `World` llamado
  `"Server"`) en vez del cliente — que es como están armados Raft/Valheim.

## Pendiente — no bloqueante

- HUD interactivo (v0.1 no tiene — `BasePlugin` no es un `MonoBehaviour` por
  defecto; requiere `ClassInjector.RegisterTypeInIl2Cpp<T>()` para registrar
  una clase adicional antes de poder usar `OnGUI`/`Update`). Por ahora, login
  solo vía config (`.cfg`) + canje automático 8s después (config
  `AutoTestRedeemOnLogin`).
- Confirmar `game_id` de VRising contra LSG (usamos `58`, heredado del
  catálogo original — no re-verificado en esta sesión).
- Confirmar el nombre exacto del `World` del servidor (`VWorld.GetServerWorld`
  asume `"Server"` y loguea `World.All` como diagnóstico la primera vez, mismo
  patrón que usamos con `Update()`/`OnGUI` en Raft/Valheim — si el nombre real
  es distinto, el log lo va a mostrar).
