# Setup — Raft.LSG.Mod

## Confirmado (2026-07-02)

- Runtime: **Mono / .NET Framework clásico** (no IL2CPP). Verificado abriendo
  `Assembly-CSharp.dll` directo en dnSpy sin necesidad de unhollowing.
- Ruta de referencia en la máquina de desarrollo (ejemplo):
  `C:\Program Files (x86)\Steam\steamapps\common\Raft\Raft_Data\Managed\`

## 1. Instalar BepInEx

```
Versión: BepInEx_win_x64_5.4.23.2 (rama 5.4.x, Mono x64)
Descarga: https://github.com/BepInEx/BepInEx/releases
```

1. Descomprimir en la raíz de Raft (junto a `Raft.exe`), **no** dentro de `Raft_Data`.
2. Ejecutar el juego una vez → genera `BepInEx/plugins/`, `BepInEx/config/`, `BepInEx/LogOutput.log`.
3. Verificar en `LogOutput.log` que el Chainloader arrancó sin errores.

## 2. Variables de entorno para compilar

No se versionan rutas absolutas de máquina de desarrollador en el `.csproj`.
Definir antes de compilar:

```powershell
setx RAFT_MANAGED_PATH "C:\Program Files (x86)\Steam\steamapps\common\Raft\Raft_Data\Managed"
setx BEPINEX_PATH "C:\Program Files (x86)\Steam\steamapps\common\Raft\BepInEx"
```

## 3. Pendiente antes de escribir `RaftEffectInterpreter`

Necesito, vía dnSpy sobre `Assembly-CSharp.dll` (ya confirmado accesible):

| Mecánica | Buscar por términos como |
|---|---|
| Paddle Speed Boost (mmv=66) | `Paddle`, `RowSpeed`, `Movement`, clase de control del remo |
| Debris Scanner (mmv=67) | `Debris`, `FloatingDebris`, `NetworkedRaftParent`, `Loot` |

Capturas de las clases/métodos relevantes (nombre de campo/propiedad que
controla la velocidad, y método que instancia/detecta escombros) son
suficientes — no hace falta el archivo completo.
