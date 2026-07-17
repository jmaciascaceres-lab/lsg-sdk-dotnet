using System;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using LSG.SDK.Core.Api;
using LSG.SDK.Core.Auth;
using LSG.SDK.Core.Config;
using LSG.SDK.Core.Mechanics;
using LSG.SDK.Core.Models;
using LSG.SDK.Core.Offline;
using VRisingLsgMod.Effects;

namespace VRisingLsgMod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BasePlugin
    {
        private const string PluginGuid = "cl.usach.diinf.lsg.vrising";
        private const string PluginName = "LSG VRising Adapter";
        private const string PluginVersion = "0.1.0";

        // Confirmado: id_videogame = 58 (db_lsg.videogame, cluster BEPINEX).
        private const int LsgGameId = 58;
        private const int MmvMovementSpeedBoost = 64;
        private const int MmvBloodQualityInsight = 65;

        private LsgConfig _config = null!;
        private LsgAuthClient _auth = null!;
        private LsgCoreApiClient _api = null!;
        private MechanicsCache _mechanics = null!;
        private OfflineQueue _offlineQueue = null!;
        private TimedEffectTracker _timedEffects = null!;
        private IDurationResolver _durationResolver = null!;
        private VRisingEffectInterpreter _interpreter = null!;

        private ConfigEntry<string> _lsgEmail = null!;
        private ConfigEntry<string> _lsgPassword = null!;
        private ConfigEntry<bool> _autoLoginOnStart = null!;
        private ConfigEntry<int> _testAttributeId = null!;
        private ConfigEntry<int> _testAmount = null!;
        private ConfigEntry<int> _bloodAttributeId = null!;
        private ConfigEntry<int> _bloodAmount = null!;
        private ConfigEntry<bool> _autoTestRedeem = null!;

        private int? _playerId;
        private System.Threading.Timer? _maintenanceTimer;
        private System.Threading.Timer? _autoTestTimer;
        private DateTimeOffset _lastOfflineFlush = DateTimeOffset.UtcNow;

        /// <summary>
        /// IL2CPP: BasePlugin.Load() reemplaza a Awake()/BaseUnityPlugin — este
        /// plugin NO es un MonoBehaviour por defecto (a diferencia de Raft/Valheim,
        /// que heredan de BaseUnityPlugin y sí lo son). Por eso todavía no hay HUD
        /// (OnGUI) acá: requeriría registrar una clase adicional en IL2CPP vía
        /// ClassInjector.RegisterTypeInIl2Cpp&lt;T&gt;() y agregarla como componente
        /// a un GameObject — pendiente para v1.0, ver README/SETUP.
        /// </summary>
        public override void Load()
        {
            BindConfig();

            _config = new LsgConfig { GameId = LsgGameId, PluginVersion = PluginVersion };
            _auth = new LsgAuthClient(_config);
            _api = new LsgCoreApiClient(_config, _auth);
            _mechanics = new MechanicsCache(_api);
            _offlineQueue = new OfflineQueue(_api, _config);
            _timedEffects = new TimedEffectTracker();
            _durationResolver = new PassthroughDurationResolver();
            _interpreter = new VRisingEffectInterpreter(Log);

            _mechanics.OnPlaceholderOptionsDetected += m =>
                Log.LogWarning($"Mecánica '{m.Name}' (mmv={m.MmvId}) sin options reales — revisar catálogo.");

            _timedEffects.OnExpired += effect =>
            {
                _interpreter.Revert(effect);
                Log.LogInfo($"Efecto expirado y revertido: {effect.Mechanic.Name} (mmv={effect.Mechanic.MmvId})");
            };

            Log.LogInfo($"{PluginName} v{PluginVersion} cargado (IL2CPP, sin Harmony — ninguna de las dos mecánicas lo necesita).");

            // Mismo mecanismo que Raft/Valheim: System.Threading.Timer puro de
            // .NET, no depende de ningún callback de Unity.
            _maintenanceTimer = new System.Threading.Timer(MaintenanceTick, null,
                TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));

            if (_autoLoginOnStart.Value && !string.IsNullOrWhiteSpace(_lsgEmail.Value) && !string.IsNullOrWhiteSpace(_lsgPassword.Value))
            {
                Log.LogInfo("AutoLoginOnStart habilitado — iniciando sesión con credenciales de BepInEx/config...");
                _ = LoginAndInitializeAsync(_lsgEmail.Value, _lsgPassword.Value);
            }
            else
            {
                Log.LogInfo("Sin credenciales en config y sin HUD todavía (v0.1) — no se puede loguear. Completa Email/Password en el .cfg.");
            }
        }

        private void BindConfig()
        {
            _lsgEmail = Config.Bind("LSG Credentials", "Email", "",
                "Email de la cuenta LSG. v0.1 no tiene HUD todavía — esta es la ÚNICA vía de login por ahora.");
            _lsgPassword = Config.Bind("LSG Credentials", "Password", "",
                "Password en texto plano. Cuenta de prueba, no producción.");
            _autoLoginOnStart = Config.Bind("LSG Credentials", "AutoLoginOnStart", true,
                "Si es true y Email/Password están completos, inicia sesión automáticamente al cargar el mod.");

            _testAttributeId = Config.Bind("LSG Test", "MovementSpeedAttributeId", 2,
                "attribute_id a debitar en el canje de Movement Speed Boost (2 = FISICO_BASE, tentativo).");
            _testAmount = Config.Bind("LSG Test", "MovementSpeedAmount", 30,
                "Monto a canjear para Movement Speed Boost.");
            _bloodAttributeId = Config.Bind("LSG Test", "BloodQualityAttributeId", 4,
                "attribute_id a debitar en el canje de Blood Quality Insight (4 = MENTAL_BASE, tentativo).");
            _bloodAmount = Config.Bind("LSG Test", "BloodQualityAmount", 25,
                "Monto a canjear para Blood Quality Insight.");

            _autoTestRedeem = Config.Bind("LSG Test", "AutoTestRedeemOnLogin", true,
                "Si es true, dispara automáticamente ambos canjes 8s después del login " +
                "(no hay HUD/botón todavía en v0.1 — ver Plugin.Load()).");
        }

        private void MaintenanceTick(object? state)
        {
            try
            {
                _timedEffects.Tick();

                if (_playerId.HasValue &&
                    (DateTimeOffset.UtcNow - _lastOfflineFlush).TotalSeconds >= _config.OfflineFlushIntervalSeconds)
                {
                    _lastOfflineFlush = DateTimeOffset.UtcNow;
                    _ = FlushOfflineQueueAsync(_playerId.Value);
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"Fallo en MaintenanceTick: {ex}");
            }
        }

        private async Task LoginAndInitializeAsync(string email, string password)
        {
            try
            {
                var session = await _auth.LoginAsync(email, password);
                _playerId = session.Player.IdPlayers;
                Log.LogInfo($"Login OK — player_id={_playerId}, roles=[{string.Join(",", session.Player.Roles)}]");

                await _mechanics.RefreshAsync();
                Log.LogInfo($"Catálogo de mecánicas cargado: {_mechanics.All.Count} mecánica(s) para game_id={LsgGameId}.");

                if (_autoTestRedeem.Value)
                {
                    Log.LogInfo("Prueba automática de canje programada en 8s (sin HUD todavía — ver nota en Load()).");
                    _autoTestTimer = new System.Threading.Timer(_ =>
                    {
                        _autoTestTimer?.Dispose();
                        if (_playerId.HasValue)
                        {
                            _ = RedeemMechanicAsync(_playerId.Value, MmvMovementSpeedBoost, _testAttributeId.Value, _testAmount.Value);
                            _ = RedeemMechanicAsync(_playerId.Value, MmvBloodQualityInsight, _bloodAttributeId.Value, _bloodAmount.Value);
                        }
                    }, null, TimeSpan.FromSeconds(8), Timeout.InfiniteTimeSpan);
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"Fallo en LoginAndInitializeAsync: {ex}");
            }
        }

        private async Task FlushOfflineQueueAsync(int playerId)
        {
            try
            {
                var pending = await _offlineQueue.FlushAsync(playerId);
                if (pending > 0)
                    Log.LogWarning($"Cola offline: {pending} evento(s) siguen pendientes tras el intento de flush.");
            }
            catch (Exception ex)
            {
                Log.LogError($"Fallo al sincronizar cola offline: {ex}");
            }
        }

        /// <summary>
        /// Ciclo de canje real: preview -> redeem -> aplicar efecto -> trackear
        /// expiración. Mismo patrón que Raft/Valheim (RedeemMechanicAsync
        /// genérico), disparado automáticamente en vez de por un botón de HUD
        /// (pendiente para v1.0).
        /// </summary>
        private async Task RedeemMechanicAsync(int playerId, int mmvId, int attributeId, int amount)
        {
            try
            {
                var mechanic = _mechanics.Get(mmvId);
                if (mechanic is null)
                {
                    Log.LogError($"mmv={mmvId} no está en el catálogo cacheado.");
                    return;
                }

                var request = new RedeemRequestDto
                {
                    ModifiableMechanicVideogameId = mmvId,
                    AttributeId = attributeId,
                    Amount = amount,
                };

                var preview = await _api.PreviewRedeemAsync(playerId, request);
                if (preview is null || !preview.CanRedeem)
                {
                    Log.LogWarning($"Saldo insuficiente para {mechanic.Name}: {preview?.CurrentBalance ?? -1} < {amount}.");
                    return;
                }

                var result = await _api.RedeemAsync(playerId, request);
                Log.LogInfo($"Redeem OK ({mechanic.Name}): ledger_id={result?.PointsLedgerId}, saldo restante={result?.ResultingBalance}.");

                var effectResult = _interpreter.Apply(mechanic);
                if (!effectResult.Success)
                {
                    Log.LogError($"Efecto no aplicado ({mechanic.Name}): {effectResult.Warning}");
                    return;
                }

                var duration = _durationResolver.Resolve(mechanic, new EffectContext { PlayerId = playerId });
                if (duration > TimeSpan.Zero)
                {
                    _timedEffects.Track(new TimedEffect
                    {
                        PlayerId = playerId,
                        Mechanic = mechanic,
                        ExpiresAt = DateTimeOffset.UtcNow + duration,
                        RevertState = effectResult.RevertState,
                    });
                    Log.LogInfo($"Efecto activo por {duration.TotalSeconds}s: {mechanic.Name}.");
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"Fallo en el ciclo de canje (mmv={mmvId}): {ex}");
            }
        }
    }
}
