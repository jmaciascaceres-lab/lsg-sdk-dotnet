# Setup — CitiesSkylines.LSG.Mod

## Confirmado

- Cities: Skylines (el original, 2015) — confirmado con el usuario.
- Runtime: Unity 5 + Mono — mismo patrón que Raft/Valheim, **no IL2CPP**.
- **API oficial de primera parte** (`ICities`) — sin BepInEx, sin Harmony.
- `game_id = 14` (LSG-Core-API, cluster `CITIES_MODAPI`).

## Cómo funciona el registro de mods (confirmado en documentación oficial)

- `IUserMod`: única interfaz obligatoria (`Name`/`Description`).
- `OnEnabled()`/`OnDisabled()`: no son parte de la interfaz, se invocan por
  reflexión si existen.
- `ThreadingExtensionBase.OnUpdate(realTimeDelta, simulationTimeDelta)`:
  hook periódico, llamado cada frame — throttleado a ~1/seg en nuestro código.
- Todas estas clases (más `EconomyExtensionBase`, `MilestonesExtensionBase`)
  se detectan automáticamente por reflexión, sin registro manual.

## Mecánicas — diseño confirmado

### Cash Income Bonus (mmv=5, existente en el catálogo real)

```csharp
public override long OnUpdateMoneyAmount(long internalMoneyAmount) { ... }
```

Confirmado en `EconomyExtensionBase` (namespace `ICities`) — mismo mecanismo
usado por el mod real "UnlimitedMoney" (confirmado vía decompile público).
Se aplica una sola vez (el hook se llama repetidamente, no solo al canjear)
usando un campo estático `EconomyExtension.PendingBonus` que se limpia tras
usarse.

**Advertencia:** el catálogo real trae `options={"Cash income bonus": "..."}`
— no un monto numérico real, y `modifiable_mechanic_description` es
literalmente `"placeholder"`. Fallback pragmático: `10000` si `options` no
trae un campo `amount`. Ver `patches/2026-08-25_milestone_unlock_boost.sql`
para completar esto en el catálogo real.

### Milestone Unlock Boost (mecánica NUEVA, no existe todavía en LSG)

```csharp
IMilestones.UnlockMilestone(string name);
IMilestones.EnumerateMilestones(); // devuelve string[]
```

Confirmado en documentación oficial — mismo mecanismo detrás del cheat
nativo "Unlock All" que trae el juego (Content Manager > Mods). El manager
(`IMilestones`) se obtiene vía `MilestonesExtensionBase.OnCreated()`, que el
juego invoca automáticamente.

**Ver `patches/2026-08-25_milestone_unlock_boost.sql`** para crear esta
mecánica en el catálogo real de LSG — el código ya está escrito y espera
`mmv` de esta mecánica (`CitiesSkylinesEffectInterpreter.MmvMilestoneUnlock`,
actualmente `-1` como placeholder — actualizar una vez creada en la BD).

**LIMITACIÓN v0.1:** no se confirmó un método para verificar qué milestones
ya están desbloqueados (solo `EnumerateMilestones()` + `UnlockMilestone()`,
sin `IsUnlocked` confirmado) — por ahora el código desbloquea el **primero**
que devuelve `EnumerateMilestones()`, sin filtrar. Pendiente de refinar una
vez que se pueda probar en juego real y ver qué strings/orden devuelve
efectivamente esa lista.

## Instalación

1. Copiar `citiesskylines.local.props.example` → `citiesskylines.local.props`,
   completar `CITIES_MANAGED_PATH`.
2. Editar `Mod.cs`: reemplazar `LsgEmail`/`LsgPassword`.
3. `dotnet build adapters\CitiesSkylines.LSG.Mod\CitiesSkylines.LSG.Mod.csproj -c Release`
4. Copiar `CitiesSkylines.LSG.Mod.merged.dll` a
   `%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\LSGCitiesSkylinesAdapter\`
5. Iniciar el juego, habilitar el mod en el Content Manager, cargar una
   partida — el canje de prueba (Cash Income Bonus) dispara solo 8s después
   del login.

## Pendiente — no bloqueante

- Sin compilar/probar todavía.
- Aplicar el patch SQL para completar `Cash income bonus` y crear
  `Milestone Unlock Boost` en el catálogo real, y actualizar el
  `MmvMilestoneUnlock` en el código con el `mmv` real generado.
- Sin comandos de consola/chat en esta API — el único disparador de prueba
  es automático tras login. Un panel real (`OnSettingsUI`) queda pendiente
  para v1.0.
