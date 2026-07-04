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

## 4. Deploy — un solo .dll (ILRepack fusiona todo automáticamente)

**Actualizado 2026-07-03:** se detectó un conflicto real en juego — Raft ya
carga su propia copia de `System.Text.Json.dll` (vía PlayFab SDK) desde
`Raft_Data/Managed/`, y Mono la resuelve antes que la nuestra en
`BepInEx/plugins/`, causando `MissingMethodException` en tiempo de
ejecución aunque el build compile perfecto. El `.csproj` ahora usa
**ILRepack** para fusionar `LSG.SDK.Core.dll` + `System.Text.Json.dll` (+
dependencias) **dentro** de `Raft.LSG.Mod.dll` con `/internalize`, así
nuestro código siempre usa su propia copia embebida sin depender del orden
de resolución de ensamblados del AppDomain.

```powershell
dotnet build adapters\Raft.LSG.Mod\Raft.LSG.Mod.csproj -c Release
copy adapters\Raft.LSG.Mod\bin\Release\netstandard2.1\Raft.LSG.Mod.merged.dll "<ruta_raft>\BepInEx\plugins\"
```

**Importante:** copiar `Raft.LSG.Mod.merged.dll` (el fusionado), **no**
`Raft.LSG.Mod.dll` (el original sin fusionar, que sigue generándose en la
misma carpeta pero depende de `LSG.SDK.Core.dll` por separado). Si ambos
terminan en `plugins/`, BepInEx probablemente falle por GUID de plugin
duplicado (`cl.usach.diinf.lsg.raft` definido dos veces) o por conflicto de
tipos. El nombre del archivo no importa para BepInEx — escanea cualquier
`.dll` en `plugins/` buscando `[BepInPlugin]`, no exige que coincida con el
nombre del ensamblado.

### 4.1. Corrección: doble ejecución de ILRepack (2026-07-03)

`ILRepack.Lib.MSBuild.Task` ejecuta su **propio** target automático
(`AfterTargets="Build"`) que fusiona todos los `.dll` de la carpeta de
salida por defecto, **salvo** que exista un archivo llamado exactamente
`ILRepack.targets` en la carpeta del proyecto — en ese caso usa ESE en vez
del suyo. Al tener nuestra lógica como un target inline dentro del
`.csproj`, ambos corrían en la misma build: el nuestro generaba
`Raft.LSG.Mod.merged.dll` correctamente, y luego el del paquete escaneaba
la carpeta, encontraba el original *y* el fusionado, e intentaba
fusionarlos entre sí → `Duplicate type RaftLsgMod.Plugin`.

**Fix:** la lógica de merge vive ahora en `adapters/Raft.LSG.Mod/ILRepack.targets`
(archivo aparte, mismo contenido de antes) — el `.csproj` solo mantiene el
`<PackageReference>`. No se requiere ningún paso adicional del lado del
desarrollador; el archivo ya está en el repo.

### 4.2. Corrección: `Microsoft.Bcl.AsyncInterfaces` y `System.Threading.Tasks.Extensions` — ni fusionar, ni omitir (2026-07-04)

`System.Text.Json` no tiene un asset específico para `netstandard2.1` — NuGet
resuelve al asset compatible con `netstandard2.0`, que **sí depende
genuinamente** de `Microsoft.Bcl.AsyncInterfaces.dll` y
`System.Threading.Tasks.Extensions.dll` en tiempo de ejecución. Estos dos
ensamblados están **deliberadamente excluidos** de la fusión de ILRepack
(fusionarlos junto a `netstandard.dll`, que ya trae `IAsyncDisposable`/
`ValueTask` nativos en netstandard2.1, generaba IL corrupto — ver nota en
`ILRepack.targets`), pero **eso no significa que sobren**: sin ellos
presentes en tiempo de ejecución, `JsonSerializer` falla con
`TypeLoadException` al inicializar `JsonTypeInfo<T>` (visto 2026-07-04).

**Deploy correcto — 3 archivos, no 1:**

```powershell
copy adapters\Raft.LSG.Mod\bin\Release\netstandard2.1\Raft.LSG.Mod.merged.dll "<ruta_raft>\BepInEx\plugins\"
copy adapters\Raft.LSG.Mod\bin\Release\netstandard2.1\Microsoft.Bcl.AsyncInterfaces.dll "<ruta_raft>\BepInEx\plugins\"
copy adapters\Raft.LSG.Mod\bin\Release\netstandard2.1\System.Threading.Tasks.Extensions.dll "<ruta_raft>\BepInEx\plugins\"
```

No copiar `Raft.LSG.Mod.dll` (el original sin fusionar) ni el resto de los
`.dll` de esa carpeta — los otros ya están internalizados en el `.merged.dll`.

## 5. Estado actual (v0.3.0)

`RaftEffectInterpreter` escrito y **smoke test confirmado en juego real**
(BepInEx cargó el plugin, `Harmony.PatchAll()` sin excepciones — log
2026-07-03). Se agregó login vía `BepInEx.Configuration` (credenciales de
prueba en `BepInEx/config/cl.usach.diinf.lsg.raft.cfg`, **no usar cuenta de
producción**) y un ciclo manual de canje disparado con `F6`
(`TestRedeemPaddleSpeedBoostAsync`) para validar de punta a punta:
login → catálogo → preview → redeem → efecto aplicado → expiración → revert.

**Pendiente de confirmar en juego:** correr con credenciales reales de
prueba, presionar F6 remando, y verificar en el log que aparezcan en orden
`Login OK`, `Redeem OK`, `Efecto activo por 900s`, y —900s después—
`Efecto expirado y revertido`. Con eso se cierra v0.3.0 según la tabla de
versionado acordada; falta solo el HUD (v1.0.0).
