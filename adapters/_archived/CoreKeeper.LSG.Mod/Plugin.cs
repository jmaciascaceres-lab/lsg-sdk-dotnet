using BepInEx;
using LSG.SDK.Core.Api;
using LSG.SDK.Core.Auth;
using LSG.SDK.Core.Config;
using LSG.SDK.Core.Mechanics;
using LSG.SDK.Core.Offline;

namespace CoreKeeper.LSG.Mod
{
    /// <summary>
    /// Entry point BepInEx. Solo compone las piezas de LSG.SDK.Core y expone el ciclo
    /// de vida del plugin. La traducción mecánica→efecto se implementa en
    /// CoreKeeperEffectInterpreter (pendiente, próximo paso).
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        private const string PluginGuid = "cl.usach.diinf.lsg.corekeeper";
        private const string PluginName = "LSG Core Keeper Adapter";
        private const string PluginVersion = "0.1.0";

        private LsgConfig _config = null!;
        private LsgAuthClient _auth = null!;
        private LsgCoreApiClient _api = null!;
        private MechanicsCache _mechanics = null!;
        private OfflineQueue _offlineQueue = null!;

        private void Awake()
        {
            _config = new LsgConfig
            {
                GameId = 16, // Core Keeper, ver videogame.id_videogame
                PluginVersion = PluginVersion,
            };

            _auth = new LsgAuthClient(_config);
            _api = new LsgCoreApiClient(_config, _auth);
            _mechanics = new MechanicsCache(_api);
            _offlineQueue = new OfflineQueue(_api, _config);

            _mechanics.OnPlaceholderOptionsDetected += m =>
                Logger.LogWarning($"Mecánica '{m.Name}' (mmv={m.MmvId}) sin options reales — revisar catálogo.");

            Logger.LogInfo($"{PluginName} v{PluginVersion} cargado. Pendiente: login interactivo y HUD.");

            // TODO (siguiente paso): flujo de login interactivo (UI in-game, no credenciales
            // hardcodeadas), luego await _mechanics.RefreshAsync(), luego registrar
            // CoreKeeperEffectInterpreter e iniciar el loop de flush de OfflineQueue.
        }
    }
}
