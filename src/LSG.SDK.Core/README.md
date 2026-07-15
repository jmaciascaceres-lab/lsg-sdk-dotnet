# LSG SDK Core

Versión: v1.1.1 (2026-07-15)

SDK-core reutilizable para conectar mods de videojuegos con el ecosistema LifeSync-Games (`lsg-auth` + `lsg-core-api`). Diseñado para el **cluster BEPINEX** (Core Keeper, Valheim, Subnautica, VRising) pero reusable en cualquier cluster C# (SMAPI, tModLoader) sin cambios.

## Principio de diseño

Este SDK **no conoce nada del juego**. Solo resuelve:

1. Autenticación JWT (login + refresh automático)
2. Perfil y saldo de puntos del jugador
3. Catálogo de mecánicas (`GET /mechanics`) con cache local
4. Canje (`redeem/preview` + `redeem`)
5. Cola offline (`POST /offline/sync`)

La traducción de una mecánica (`buff`, `modifier`, ...) a la mecánica real del juego (Harmony patch, evento SMAPI, etc.) la implementa **cada adaptador de juego** vía `IEffectInterpreter`. Esto es lo que permite mantener cada mod de forma independiente, con distintos ciclos de release, sin tocar este SDK.

```
LSG.SDK.Core (este repo)
  ├── Auth → LsgAuthClient
  ├── Api → LsgCoreApiClient
  ├── Mechanics → MechanicsCache, IEffectInterpreter
  ├── Offline → OfflineQueue
  └── Models → DTOs

Raft.LSG.Mod (repo aparte, referencia este SDK) - CERRADO v1.1.1
  └── RaftEffectInterpreter : ITimedEffectInterpreter
        - Paddle Speed Boost (mmv=66) → Harmony patch sobre Paddle.PaddlePaddle
        - Loot Luck Boost (mmv=67) → Harmony patch sobre SO_MysteryPackageLoot.GetRandomItemFromPossibles (redefinido como garantía de ítem, no "más rareza" - el juego no soporta eso)

Valheim.LSG.Mod (repo aparte) - validado end-to-end v0.2.0
  └── ValheimEffectInterpreter : ITimedEffectInterpreter
        - Stamina Regen Boost (mmv=60) → SEMan.AddStatusEffect (sistema NATIVO, sin Harmony)
        - Comfort Boost (mmv=61) → Harmony patch sobre SE_Rested.CalculateComfortLevel
```

## Contrato de referencia - mecánicas mínimas cargadas (2026-07-02)

| Juego (id) | mmv_id | Nombre | Tipo | Dimensión objetivo |
|---|---|---|---|---|
| Core Keeper (16) | 58 | Mining Speed Boost | buff | FISICO_BASE |
| Core Keeper (16) | 59 | Reveal Nearby Map | modifier | MENTAL_BASE |
| Valheim (17) | 60 | Stamina Regen Boost | buff | FISICO_BASE |
| Valheim (17) | 61 | Comfort Boost | buff | MENTAL_BASE |
| Subnautica (19) | 62 | Oxygen Capacity Boost | buff | FISICO_BASE |
| Subnautica (19) | 63 | Scanner Radius Boost | modifier | MENTAL_BASE |
| VRising (58) | 64 | Movement Speed Boost | buff | FISICO_BASE |
| VRising (58) | 65 | Blood Quality Insight | modifier | MENTAL_BASE |
| Raft (71) | 66 | Paddle Speed Boost | buff | FISICO_BASE |
| Raft (71) | 67 | ~~Debris Scanner~~ → **Loot Luck Boost** (renombrado 2026-07-03: Debris Scanner no correspondía a ningún sistema real de Raft) | modifier | MENTAL_BASE |

> **Nota de calidad de datos:** Subnautica (game_id=19) tiene 7 mecánicas legacy previas (mmv 35-43) con `options` placeholder (`{"additionalProp1": {}}`). `MechanicsCache` las detecta vía `HasPlaceholderOrEmptyOptions()` y dispara `OnPlaceholderOptionsDetected` para que el adaptador decida (loguear, ignorar, o excluir del HUD hasta que se limpien en el catálogo).

## Uso típico (pseudo-flujo de un adaptador)

```csharp
var config = new LsgConfig { GameId = 17 /* Valheim */, PluginVersion = "0.1.0" };
var auth = new LsgAuthClient(config);
var api = new LsgCoreApiClient(config, auth);
var mechanics = new MechanicsCache(api);
var offline = new OfflineQueue(api, config);

mechanics.OnPlaceholderOptionsDetected += m =>
    Log.Warn($"Mecánica '{m.Name}' (mmv={m.MmvId}) sin options reales - revisar catálogo.");

// 1. Login (una vez, al iniciar el mod)
var session = await auth.LoginAsync(playerEmail, playerPassword);
var playerId = session.Player.IdPlayers;

// 2. Cache del catálogo (una vez, o al reconectar)
await mechanics.RefreshAsync();

// 3. Jugador intenta canjear "Comfort Boost" (mmv=61)
var req = new RedeemRequestDto { ModifiableMechanicVideogameId = 61, AttributeId = 4 /* MENTAL */, Amount = 25 };
var preview = await api.PreviewRedeemAsync(playerId, req);

if (preview!.CanRedeem)
{
    var result = await api.RedeemAsync(playerId, req);
    var mechanic = mechanics.Get(61)!;
    var effect = effectInterpreter.Apply(mechanic); // implementado por el adaptador de Valheim
}

// 4. Si el jugador estuvo offline, encolar y flushear periódicamente
offline.Enqueue(new OfflineEventDto { PointDimensionId = 2, Direction = "CREDIT", Amount = 10 });
await offline.FlushAsync(playerId);
```

## Efectos con duración variable (`buff` con `duration_seconds`)

La duración base viene del catálogo (`options.duration_seconds`), pero puede necesitar escalarse por juego/dificultad. Se resuelve con tres piezas desacopladas, todas en `Mechanics/`:

- **`IGameClock`** - fuente de tiempo (default `SystemClock` = reloj real).
- **`IDurationResolver`** - traduce duración base → duración efectiva. Default `PassthroughDurationResolver` no escala nada. Un adaptador que necesite ajustar por dificultad implementa su propio resolver:

  ```csharp
  public sealed class CoreKeeperDurationResolver : IDurationResolver
  {
      public TimeSpan Resolve(MechanicDto mechanic, EffectContext ctx)
      {
          var baseSeconds = new PassthroughDurationResolver().Resolve(mechanic, ctx).TotalSeconds;
          var factor = ctx.DifficultyTag switch
          {
              "hardcore" => 0.5,   // buffs duran la mitad en hardcore
              "peaceful" => 1.5,
              _ => 1.0,
          };
          return TimeSpan.FromSeconds(baseSeconds * factor);
      }
  }
  ```

- **`ITimedEffectTracker`** (`TimedEffectTracker`) - trackea expiración y dispara `OnExpired`. No sabe qué es un "buff" ni cómo revertirlo.
- **`ITimedEffectInterpreter`** - extiende `IEffectInterpreter` con `Revert(TimedEffect)`. El adaptador, al aplicar un `buff`, guarda en `RevertState` lo necesario para deshacerlo (ej. valor original antes del multiplicador) y lo recibe de vuelta cuando el tracker dispara `OnExpired`.

```csharp
var duration = durationResolver.Resolve(mechanic, ctx);
if (duration > TimeSpan.Zero)
{
    var result = interpreter.Apply(mechanic); // adaptador aplica y llena RevertState
    tracker.Track(new TimedEffect
    {
        PlayerId = playerId,
        Mechanic = mechanic,
        ExpiresAt = clock.UtcNow + duration,
        RevertState = result.RevertState,
    });
}
// En el game loop del adaptador:
tracker.OnExpired += effect => interpreter.Revert(effect);
// tracker.Tick() llamado desde Update()/heartbeat del mod-loader.
```

**Limitación conocida:** `TimedEffectTracker` es en memoria - si el proceso del mod se reinicia (crash, alt-F4), los efectos activos se pierden sin revertirse. Aceptable para v1 (impacto: el jugador conserva el buff hasta el próximo reinicio en vez de perderlo a tiempo). Si se vuelve un problema real, la solución es persistir `TimedEffect` en un archivo local del adaptador y rehidratar el tracker en `Awake()`.

## Nota de compatibilidad con Mono viejo - resuelta migrando a Newtonsoft.Json (2026-07-05)

`System.Text.Json` moderno (`DeserializeAsync`/`ValueTask`, `IAsyncDisposable`, `JsonTypeInfo<T>`, `Utf8JsonWriter`) produjo **cinco fallas distintas** en el Mono de BepInEx 5.4.x (CLR 4.0.30319, era .NET Framework 4.x) a lo largo del desarrollo - todas por la misma causa raíz: el despacho genérico virtual complejo de esa librería no es compatible con el JIT de ese runtime tan viejo. No era un problema de ILRepack ni de conflicto de ensamblados.

**Se migró todo el SDK-core a `Newtonsoft.Json`** (`JsonConvert.SerializeObject`/`DeserializeObject`, atributos `[JsonProperty]`, `JToken`/`JObject` en vez de `JsonElement`) - el estándar de facto en modding BepInEx/Unity/Mono precisamente por no tener esta complejidad arquitectónica. Si se agrega un nuevo modelo o método al cliente HTTP, usar Newtonsoft.Json - no reintroducir `System.Text.Json` en este proyecto.

## Misterio resuelto: `Update()`/`Start()`/`OnGUI()` nunca se ejecutaban (2026-07-10)

**Causa real:** el `GameObject` administrador de BepInEx era destruido por el propio juego durante la transición a `MainScene`, justo después de que `Awake()`/`OnEnable()` ya habían corrido (por eso esos sí se veían en el log) pero antes de la primera oportunidad de `Start()`/`Update()`/`OnGUI()`. Mismo patrón documentado en BepInEx/BepInEx#420 y BepInEx/BepInEx#827.

**Fix (sin tocar código):** en `BepInEx/config/BepInEx.cfg`, sección `[Preloader]`, cambiar `HideManagerGameObject = false` → `true`. Con esto:
- `OnGUI()` corre normalmente - el HUD es visible en pantalla.
- Los workarounds con `System.Threading.Timer` (mantenimiento periódico) siguen funcionando y no hace falta revertirlos - son robustos de todas formas y no dependen de que el ciclo de vida de Unity funcione.

**v1.0.0 validado en juego real (2026-07-10):** login interactivo vía HUD, saldo mostrado y refrescado, canje de Paddle Speed Boost disparado desde el botón del HUD, efecto aplicado (confirmado con logs objetivos de `PaddleForcePatch`), y contador de tiempo restante en pantalla - ciclo completo end-to-end.

## Pendientes conocidos

- `OfflineQueue.FlushAsync` trata la respuesta 207 como éxito global; una iteración futura debe parsear el detalle por evento (`SYNCED` / `DUPLICATE` / `REJECTED`) y re-encolar solo los rechazados por causa transitoria.
- `IEffectInterpreter` no define aún un mecanismo de rollback si `Apply()` falla después de un `redeem` exitoso (puntos ya debitados, efecto no aplicado). Decisión pendiente: ¿reintento local, o endpoint de compensación en el core? A discutir antes de M3.

## Referencias

- R. González-Ibáñez, J. I. Macías-Cáceres and M. V. Paucar, "LifeSync-Games: A Technical Note on a Novel Framework for Video Game Development," 2025 44th International Conference of the Chilean Computer Science Society (SCCC), Valparaiso, Chile, 2025, pp. 1-4, doi: 10.1109/SCCC67219.2025.11420722.
- González-Ibáñez R., Macías-Cáceres J., Villalta-Paucar M. (2025). LifeSync-Games: Toward a Video Game Paradigm for Promoting Responsible Gaming and Human Development. arXiv:2510.19691 [cs.HC]. DOI: https://arxiv.org/abs/2510.19691
