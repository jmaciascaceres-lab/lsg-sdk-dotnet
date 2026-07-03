# Setup - Raft.LSG.Mod

### Version: v0.2.1 (2026-07-03)

- Runtime: **Mono / .NET Framework clásico** (no IL2CPP). Verificado abriendo `Assembly-CSharp.dll` directo en dnSpy sin necesidad de unhollowing.
- Ruta de referencia en la máquina de desarrollo (ejemplo): `C:\Program Files (x86)\Steam\steamapps\common\Raft\Raft_Data\Managed\`

## 1. Instalar BepInEx

```
Versión: BepInEx_win_x64_5.4.23.2 (rama 5.4.x, Mono x64)
Descarga: https://github.com/BepInEx/BepInEx/releases
```

1. Descomprimir en la raíz de Raft (junto a `Raft.exe`), **no** dentro de `Raft_Data`.
2. Ejecutar el juego una vez → genera `BepInEx/plugins/`, `BepInEx/config/`, `BepInEx/LogOutput.log`.
3. Verificar en `LogOutput.log` que el Chainloader arrancó sin errores.

## 2. Configurar rutas locales (sin depender de variables de entorno de sesión)

`setx` solo aplica a terminales **nuevas** — si ya tenías PowerShell/VS Code abierto cuando lo corriste, esa sesión no lo ve, y es fácil perder tiempo
pensando que el build está roto cuando en realidad falta reabrir la terminal. Para evitar ese problema (y que se repita con cada tesista/adaptador nuevo),
usamos un archivo de propiedades local, no versionado:

```powershell
copy raft.local.props.example raft.local.props
notepad raft.local.props   # editar con tus rutas reales
```

`raft.local.props` está en `.gitignore` — cada quien mantiene sus propias rutas sin pisar las de otros ni depender del estado de la terminal. Si el archivo no existe, el `.csproj` cae de vuelta a las variables de entorno `RAFT_MANAGED_PATH`/`BEPINEX_PATH` (mecanismo anterior, sigue funcionando como fallback).

## 4. Deploy — copiar TODA la carpeta de output, no un solo .dll

`Raft.LSG.Mod.dll` depende de `LSG.SDK.Core.dll` (ProjectReference) y de `System.Text.Json.dll` + sus dependencias transitivas (PackageReference). Copiar solo `Raft.LSG.Mod.dll` produce exactamente el síntoma `"The script 'RaftLsgMod.Plugin' could not be instantiated!"` en el log de Unity — el CLR no puede resolver los tipos base porque el ensamblado que los define no está presente.

```powershell
dotnet build adapters\Raft.LSG.Mod\Raft.LSG.Mod.csproj -c Release
xcopy /Y adapters\Raft.LSG.Mod\bin\Release\netstandard2.1\*.dll "<ruta_raft>\BepInEx\plugins\"
```

El `.csproj` tiene `CopyLocalLockFileAssemblies=true`, así que el build ya deja todos los `.dll` necesarios (incluido `LSG.SDK.Core.dll`) en esa misma carpeta de output — el `xcopy` de arriba los copia todos de una vez.

## 5. Changelog

### v0.2.1 (2026-07-03)

- Se agrego `RaftEffectInterpreter` ya está escrito (`Paddle Speed Boost` vía Harmony patch sobre `Paddle.PaddlePaddle`, `Loot Luck Boost` como placeholder no-op). Build limpio, sin errores ni warnings. Pendiente: smoke test en juego real (ver troubleshooting de deploy arriba), luego login interactivo y conexión del flujo de `redeem`.

## Referencias

- R. González-Ibáñez, J. I. Macías-Cáceres and M. V. Paucar, "LifeSync-Games: A Technical Note on a Novel Framework for Video Game Development," 2025 44th International Conference of the Chilean Computer Science Society (SCCC), Valparaiso, Chile, 2025, pp. 1-4, doi: 10.1109/SCCC67219.2025.11420722.
- González-Ibáñez R., Macías-Cáceres J., Villalta-Paucar M. (2025). LifeSync-Games: Toward a Video Game Paradigm for Promoting Responsible Gaming and Human Development. arXiv:2510.19691 [cs.HC]. DOI: https://arxiv.org/abs/2510.19691
