# ARCHIVADO — 2026-07-02

Este adaptador fue **descartado antes de implementar `IEffectInterpreter`**.

## Motivo

Core Keeper migró su modding oficial a un SDK propio distribuido vía
mod.io. El camino no oficial (BepInEx + Harmony, que es el que asume este
scaffold) queda como vía secundaria/inestable, sin garantía de acceso
directo a memoria/red equivalente al de un mod BepInEx clásico —
riesgo similar al que descartó a Starbound (sandbox restringido).

## Estado del juego en LSG

`videogame.id_videogame = 16`, marcado en BD como:
`[DEPRECATED:MODDING_INFEASIBLE] [CLUSTER:BEPINEX]`

**No se eliminó el registro** (ni sus dos mecánicas `mmv=58,59`) porque ya
existían pruebas reales de `redeem/preview` contra ese `game_id` durante
el desarrollo del SDK-core — se conserva por trazabilidad e integridad
referencial, y como insumo documental de "juego evaluado y descartado"
para la sección de metodología de la tesis.

## Reemplazo

**Raft** (`adapters/Raft.LSG.Mod/`), mismo cluster `BEPINEX`.

## Qué se conserva de este trabajo

El diseño de `Plugin.cs` (composición de `LsgAuthClient` /
`LsgCoreApiClient` / `MechanicsCache` / `OfflineQueue` en `Awake()`) y la
intención de mecánicas físico/mental (velocidad de acción + revelar
información) se reutilizan como plantilla directa para Raft — ver
`adapters/Raft.LSG.Mod/`.
