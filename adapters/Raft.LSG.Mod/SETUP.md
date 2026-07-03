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

## 2. Configurar rutas locales (sin depender de variables de entorno de sesión)

`setx` solo aplica a terminales **nuevas** — si ya tenías PowerShell/VS Code
abierto cuando lo corriste, esa sesión no lo ve, y es fácil perder tiempo
pensando que el build está roto cuando en realidad falta reabrir la terminal.
Para evitar ese problema (y que se repita con cada tesista/adaptador nuevo),
usamos un archivo de propiedades local, no versionado:

```powershell
copy raft.local.props.example raft.local.props
notepad raft.local.props   # editar con tus rutas reales
```

`raft.local.props` está en `.gitignore` — cada quien mantiene sus propias
rutas sin pisar las de otros ni depender del estado de la terminal. Si el
archivo no existe, el `.csproj` cae de vuelta a las variables de entorno
`RAFT_MANAGED_PATH`/`BEPINEX_PATH` (mecanismo anterior, sigue funcionando
como fallback).

## 3. Pendiente antes de escribir `RaftEffectInterpreter`

Necesito, vía dnSpy sobre `Assembly-CSharp.dll` (ya confirmado accesible):

| Mecánica | Buscar por términos como |
|---|---|
| Paddle Speed Boost (mmv=66) | `Paddle`, `RowSpeed`, `Movement`, clase de control del remo |
| Debris Scanner (mmv=67) | `Debris`, `FloatingDebris`, `NetworkedRaftParent`, `Loot` |

Capturas de las clases/métodos relevantes (nombre de campo/propiedad que
controla la velocidad, y método que instancia/detecta escombros) son
suficientes — no hace falta el archivo completo.
