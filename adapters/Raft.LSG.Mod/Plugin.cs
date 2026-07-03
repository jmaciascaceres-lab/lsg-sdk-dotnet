using BepInEx;
using HarmonyLib;
using LSG.SDK.Core.Api;
using LSG.SDK.Core.Auth;
using LSG.SDK.Core.Config;
using LSG.SDK.Core.Mechanics;
using LSG.SDK.Core.Offline;
using Raft.LSG.Mod.Effects;

namespace Raft.LSG.Mod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        private const string PluginGuid = "cl.usach.diinf.lsg.raft";
        private const string PluginName = "LSG Raft Adapter";
        private const string PluginVersion = "0.2.0";

        // Confirmado: id_videogame = 71 (db_lsg.videogame, 2026-07-02).
        private const int LsgGameId = 71;

        private LsgConfig _config = null!;
        private LsgAuthClient _auth = null!;
        private LsgCoreApiClient _api = null!;
        private MechanicsCache _mechanics = null!;
        private OfflineQueue _offlineQueue = null!;
        private TimedEffectTracker _timedEffects = null!;
        private IDurationResolver _durationResolver = null!;
        private RaftEffectInterpreter _interpreter = null!;
        private Harmony _harmony = null!;

        private void Awake()
        {
            _config = new LsgConfig { GameId = LsgGameId, PluginVersion = PluginVersion };
            _auth = new LsgAuthClient(_config);
            _api = new LsgCoreApiClient(_config, _auth);
            _mechanics = new MechanicsCache(_api);
            _offlineQueue = new OfflineQueue(_api, _config);
            _timedEffects = new TimedEffectTracker();
            _durationResolver = new PassthroughDurationResolver();
            _interpreter = new RaftEffectInterpreter();

            _mechanics.OnPlaceholderOptionsDetected += m =>
                Logger.LogWarning($"Mecánica '{m.Name}' (mmv={m.MmvId}) sin options reales — revisar catálogo.");

            _timedEffects.OnExpired += effect =>
            {
                _interpreter.Revert(effect);
                Logger.LogInfo($"Efecto expirado y revertido: {effect.Mechanic.Name} (mmv={effect.Mechanic.MmvId})");
            };

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            Logger.LogInfo($"{PluginName} v{PluginVersion} cargado. Patch de Harmony aplicado. Pendiente: login interactivo, HUD, redeem real conectado al flujo del jugador.");

            // TODO (siguiente paso):
            //   1. Login interactivo (UI in-game)
            //   2. await _mechanics.RefreshAsync()
            //   3. Al confirmar redeem exitoso (mmv=66):
            //        var result = _interpreter.Apply(mechanic);
            //        var duration = _durationResolver.Resolve(mechanic, new EffectContext { PlayerId = playerId });
            //        if (duration > TimeSpan.Zero)
            //            _timedEffects.Track(new TimedEffect { PlayerId = playerId, Mechanic = mechanic,
            //                ExpiresAt = DateTimeOffset.UtcNow + duration, RevertState = result.RevertState });
            //   4. Hook a Update() para _timedEffects.Tick() y flush periódico de _offlineQueue
        }
    }
}
