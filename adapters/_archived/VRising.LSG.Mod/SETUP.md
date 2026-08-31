# Setup — VRising.LSG.Mod

## ESTADO: PAUSADO (2026-08-25)

Curva de integración desproporcionadamente alta frente a Raft/Valheim — IL2CPP
(no Mono), servidor como proceso separado del cliente, y un crash nativo de
marshaling sin resolver en el último tramo. Se decide pausar y priorizar el
resto del catálogo (Subnautica, StardewValley/Terraria) en vez de seguir
invirtiendo tiempo acá. Todo lo aprendido queda documentado abajo para
retomarlo si en algún momento vuelve a ser prioridad — la mayor parte del
camino difícil (compilación, despliegue, diseño de mecánicas, acceso a
`World`/`Entity`) ya está resuelta; solo falta destrabar el último punto.

## Qué SÍ se logró (no hay que repetirlo)

- El proyecto **compila** completo contra IL2CPP (ver todas las referencias
  correctas al `.csproj` más abajo).
- **Despliega y corre en juego real**: login contra LSG, catálogo de
  mecánicas, `preview`/`redeem` con saldo real — todo confirmado en logs con
  ledger_id reales (ej. `ledger_id=2842`).
- Se encontró el flujo de arranque que efectivamente funciona (ver sección
  "Arquitectura de despliegue").
- Se confirmó el `World` "Server" (`World.All disponibles: [Default World,
  LoadingWorld0, Server, LoadingWorld0]`) y el diseño de ambas mecánicas sin
  necesitar Harmony.
- Se resolvió cómo llegar de un `User` conectado a la `Entity` de su
  personaje (`User.LocalCharacter.TryGetSyncedEntity`).

## Qué quedó bloqueando (el motivo de la pausa)

**Crash nativo (`AccessViolationException`) dentro de
`Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke`**, disparado al llamar
`EntityManager.CreateEntityQuery(...)` — confirmado dos veces en
`ErrorLog.log`, con el mismo stack trace exacto ambas veces, incluso tras
cambiar de pasar un array de `ComponentType` a pasar uno suelto (el compilador
lo empaquetaba igual en un `Il2CppStructArray<ComponentType>` por ser el único
overload disponible en este binding).

**Último intento sin probar en juego** (quedó escrito en `VWorld.cs`, sin
confirmar si funciona): reemplazar `CreateEntityQuery` por
`EntityQueryBuilder.WithAll<User>()` — usa un método **genérico** (mismo
patrón que `GetComponentData<User>()`, que sí funciona sin problema en los
mismos logs) en vez de un array de `ComponentType`, para evitar por completo
el camino de marshaling que crashea. Si se retoma este adaptador, este es el
punto exacto por donde seguir.

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
| Proceso del servidor | embebido en el mismo proceso | **proceso separado** (`VRisingServer.exe`), incluso en un jugador |

## Historial completo de gotchas (para no repetir la investigación)

### 1. Il2CppDumper — correr desde la carpeta del juego, no desde otra

Error `Win32Exception (126): No se puede encontrar el módulo especificado`
al cargar `GameAssembly.dll`. **Fix:** copiar `Il2CppDumper.exe` (y sus
archivos acompañantes) directo a la carpeta de VRising y ejecutarlo desde ahí.

### 2. BepInEx 6.x genérico (GitHub) falla con Cpp2IL en VRising

Error `Failed to find Binary code or metadata registration` — bug conocido
del repo de BepInEx (issues #866, #877, #879) contra la build genérica.

**Fix: usar `BepInExPack_V_Rising` de Thunderstore, NO el BepInEx genérico de
GitHub**:
```
https://v-rising.thunderstore.io/package/BepInEx/BepInExPack_V_Rising/
```

### 3. Referencias del `.csproj` — repartidas en varias carpetas y ensamblados

- `BepInEx/core/` → `BepInEx.Core.dll` (¡no `BepInEx.dll`!),
  `BepInEx.Unity.Common.dll`, `BepInEx.Unity.IL2CPP.dll`,
  `Il2CppInterop.Runtime.dll`, `0Harmony.dll`
- `BepInEx/interop/` → `ProjectM*.dll` (repartido en ~25-30 ensamblados),
  `ProjectM.CodeGeneration.dll` (contiene `NetworkedEntity`),
  `Stunlock.Core.dll` (contiene `PrefabGUID`), `Il2Cppmscorlib.dll`
  (tipos base `System.Object`/`ValueType` propios de IL2CPP — requisito
  universal para cualquier proyecto con tipos generados por Il2CppInterop),
  `Unity.Entities.dll`, `Unity.Collections.dll`, `UnityEngine.CoreModule.dll`
- Il2CppDumper (carpeta aparte) → **solo para leer en dnSpy**, nunca para compilar

### 4. Doorstop no se inyecta en `VRisingServer.exe` con `winhttp.dll`

`VRisingServer.exe` corre `-batchMode -nographics` (headless) y no carga
`winhttp.dll` de forma natural, así que el gancho de doorstop nunca se activa
con ese nombre. **Fix (documentado en la guía oficial de troubleshooting de
BepInEx):** renombrar `winhttp.dll` → `version.dll` en `VRising_Server\`.

### 5. Arquitectura de despliegue — servidor como proceso separado

VRising **siempre** lanza `VRisingServer.exe` como proceso hijo separado,
incluso en partidas de un jugador. `DebugEventsSystem`/`BuffUtility`/`User`
viven en el `World` "Server" de **ese** proceso — el plugin debe instalarse en
`VRising_Server\BepInEx\plugins\`, no en `VRising\BepInEx\plugins\`.

### 6. Flujo de arranque que efectivamente funciona

Ni "todo automático" ni "todo manual" funcionan solos — hace falta la
combinación:

1. Lanzar `VRisingServer.exe` **manualmente** (doble clic directo) — lanzado
   como hijo del cliente, el gancho de doorstop no se activa por una razón no
   confirmada.
2. Esperar a que cargue completo (logs de LSG visibles en su consola).
3. Abrir `VRising.exe` **desde Steam** (no el `.exe` directo — sin pasar por
   Steam, falla con "STEAMWORKS INITIALISATION FAILED").
4. Continuar la partida existente (no "nueva partida").
5. Ojo: el servidor manual guarda en `...\VRisingServer\Saves\` (con sufijo
   `Server`), distinto de `...\VRising\Saves\` del cliente normal.

## Mecánicas: diseño confirmado, sin necesidad de Harmony

| Mecánica (mmv) | Mecanismo |
|---|---|
| Movement Speed Boost (64) | `DebugEventsSystem.ApplyBuff` con `PrefabGUID` de un buff de velocidad existente (`-911970381`, "Voltatia's Electric Speed Buff" — reportado por la comunidad, no verificado en nuestro propio dump ya que es un dato de contenido, no código), luego sobreescribir `ModifyMovementSpeedBuff.MoveSpeed` en la Entity resultante |
| Blood Quality Insight (65) | Escritura directa de `Blood.Quality` (`IComponentData` simple) en la Entity del jugador |

Patrón de `DebugEventsSystem`/`BuffUtility` adaptado de **KindredCommands**
(mod open-source, AGPL-3.0, `Buffs.cs`) — no copiado literal.

## Resolución de Entity del jugador — diseño resuelto, ejecución bloqueada por el crash

`ProjectM.Network.User : IComponentData` tiene `LocalCharacter`
(`NetworkedEntity`, resuelto vía `TryGetSyncedEntity(out Entity)`),
`PlatformId` (Steam ID), `IsConnected` (bool). El plan es recorrer un
`EntityQuery`/`EntityQueryBuilder` sobre `User` y usar el `LocalCharacter` del
primer conectado — pero construir esa query es justo lo que crashea (ver
sección de arriba).

**Limitación v0.1, ya decidida independiente del crash:** LSG no tiene el
Steam ID del jugador en la base de datos, así que el match real
`player_id -> PlatformId -> User` no es posible todavía — el plan siempre fue
usar "primer `User` conectado" como atajo de v0.1, válido solo para pruebas
con una única cuenta a la vez.

## Pendiente — no bloqueante (si se retoma)

- HUD interactivo (v0.1 no tiene — requiere `ClassInjector.RegisterTypeInIl2Cpp<T>()`).
- Confirmar `game_id` de VRising contra LSG (usamos `58`, no re-verificado).
- `VRisingEffectInterpreter.Revert()` vacío — Blood Quality Insight no
  revierte su bonus al expirar.
