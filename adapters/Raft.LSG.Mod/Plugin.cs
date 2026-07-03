using BepInEx;
using LSG.SDK.Core.Api;
using LSG.SDK.Core.Auth;
using LSG.SDK.Core.Config;
using LSG.SDK.Core.Mechanics;
using LSG.SDK.Core.Offline;

namespace Raft.LSG.Mod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        private const string PluginGuid = "cl.usach.diinf.lsg.raft";
        private const string PluginName = "LSG Raft Adapter";
        private const string PluginVersion = "0.1.0";

        // Confirmado: id_videogame = 71 (db_lsg.videogame, 2026-07-02).
        private const int LsgGameId = 71;

        private LsgConfig _config = null!;
        private LsgAuthClient _auth = null!;
        private LsgCoreApiClient _api = null!;
        private MechanicsCache _mechanics = null!;
        private OfflineQueue _offlineQueue = null!;
        private TimedEffectTracker _timedEffects = null!;

        private void Awake()
        {
            _config = new LsgConfig { GameId = LsgGameId, PluginVersion = PluginVersion };
            _auth = new LsgAuthClient(_config);
            _api = new LsgCoreApiClient(_config, _auth);
            _mechanics = new MechanicsCache(_api);
            _offlineQueue = new OfflineQueue(_api, _config);
            _timedEffects = new TimedEffectTracker();

            _mechanics.OnPlaceholderOptionsDetected += m =>
                Logger.LogWarning($"Mecánica '{m.Name}' (mmv={m.MmvId}) sin options reales — revisar catálogo.");

            Logger.LogInfo($"{PluginName} v{PluginVersion} cargado. Pendiente: login interactivo, HUD e IEffectInterpreter.");

            // TODO (siguiente paso, análogo a Core Keeper):
            //   1. Login interactivo (UI in-game)
            //   2. await _mechanics.RefreshAsync()
            //   3. Registrar RaftEffectInterpreter : ITimedEffectInterpreter
            //   4. _timedEffects.OnExpired += e => raftInterpreter.Revert(e);
            //   5. Hook a Update() para _timedEffects.Tick() y flush periódico de _offlineQueue
        }
    }
}
