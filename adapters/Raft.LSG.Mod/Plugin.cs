using System;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using LSG.SDK.Core.Api;
using LSG.SDK.Core.Auth;
using LSG.SDK.Core.Config;
using LSG.SDK.Core.Mechanics;
using LSG.SDK.Core.Models;
using LSG.SDK.Core.Offline;
using RaftLsgMod.Effects;
using UnityEngine;

namespace RaftLsgMod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        private const string PluginGuid = "cl.usach.diinf.lsg.raft";
        private const string PluginName = "LSG Raft Adapter";
        private const string PluginVersion = "0.3.0";

        // Confirmado: id_videogame = 71 (db_lsg.videogame, 2026-07-02).
        private const int LsgGameId = 71;
        private const int MmvPaddleSpeedBoost = 66;

        private LsgConfig _config = null!;
        private LsgAuthClient _auth = null!;
        private LsgCoreApiClient _api = null!;
        private MechanicsCache _mechanics = null!;
        private OfflineQueue _offlineQueue = null!;
        private TimedEffectTracker _timedEffects = null!;
        private IDurationResolver _durationResolver = null!;
        private RaftEffectInterpreter _interpreter = null!;
        private Harmony _harmony = null!;

        private ConfigEntry<string> _lsgEmail = null!;
        private ConfigEntry<string> _lsgPassword = null!;
        private ConfigEntry<int> _testAttributeId = null!;
        private ConfigEntry<int> _testAmount = null!;
        private ConfigEntry<KeyCode> _testRedeemKey = null!;

        private int? _playerId;
        private bool _isRedeemInFlight;

        private void Awake()
        {
            BindConfig();

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

            Logger.LogInfo($"{PluginName} v{PluginVersion} cargado. Patch de Harmony aplicado. Iniciando login...");

            // Awake() no puede ser async (BepInEx/Unity no lo soportan) — fire-and-forget
            // con captura explícita de excepciones para que nunca quede una Task sin observar.
            _ = InitializeAsync();
        }

        private void BindConfig()
        {
            // ADVERTENCIA DE SEGURIDAD (aceptado para esta fase de pruebas manuales):
            // la contraseña queda en texto plano en BepInEx/config/cl.usach.diinf.lsg.raft.cfg.
            // NO usar credenciales reales de producción para este smoke test — usar una
            // cuenta de prueba. Reemplazar por login interactivo (UI in-game) antes de v1.0.
            _lsgEmail = Config.Bind("LSG Credentials", "Email", "",
                "Email de la cuenta LSG (cuenta de prueba, no producción).");
            _lsgPassword = Config.Bind("LSG Credentials", "Password", "",
                "Password en texto plano — solo para pruebas manuales. Reemplazar por login in-game antes de v1.0.");

            _testAttributeId = Config.Bind("LSG Test", "TestAttributeId", 2,
                "attribute_id a debitar en el canje de prueba (2 = FISICO_BASE, tentativo — confirmar contra /attributes).");
            _testAmount = Config.Bind("LSG Test", "TestAmount", 30,
                "Monto a canjear en la prueba manual (coincide con el costo sugerido de Paddle Speed Boost).");
            _testRedeemKey = Config.Bind("LSG Test", "TestRedeemKey", KeyCode.F6,
                "Tecla para disparar un canje manual de prueba de Paddle Speed Boost (mmv=66).");
        }

        private async Task InitializeAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_lsgEmail.Value) || string.IsNullOrWhiteSpace(_lsgPassword.Value))
                {
                    Logger.LogWarning("Email/Password no configurados en BepInEx/config/cl.usach.diinf.lsg.raft.cfg — login omitido.");
                    return;
                }

                var session = await _auth.LoginAsync(_lsgEmail.Value, _lsgPassword.Value);
                _playerId = session.Player.IdPlayers;
                Logger.LogInfo($"Login OK — player_id={_playerId}, roles=[{string.Join(",", session.Player.Roles)}]");

                await _mechanics.RefreshAsync();
                Logger.LogInfo($"Catálogo de mecánicas cargado: {_mechanics.All.Count} mecánica(s) para game_id={LsgGameId}.");

                // Flush periódico de la cola offline, una vez que hay player_id.
                InvokeRepeating(nameof(FlushOfflineQueueTick), _config.OfflineFlushIntervalSeconds, _config.OfflineFlushIntervalSeconds);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Fallo en InitializeAsync: {ex}");
            }
        }

        private void Update()
        {
            _timedEffects.Tick();

            if (_playerId.HasValue && !_isRedeemInFlight && Input.GetKeyDown(_testRedeemKey.Value))
            {
                _ = TestRedeemPaddleSpeedBoostAsync(_playerId.Value);
            }
        }

        /// <summary>
        /// Invocado por InvokeRepeating (API de Unity, no requiere coroutines).
        /// No puede ser async void directamente por buenas prácticas, así que delega
        /// a un Task fire-and-forget con manejo de excepciones.
        /// </summary>
        private void FlushOfflineQueueTick()
        {
            if (!_playerId.HasValue)
                return;

            _ = FlushOfflineQueueAsync(_playerId.Value);
        }

        private async Task FlushOfflineQueueAsync(int playerId)
        {
            try
            {
                var pending = await _offlineQueue.FlushAsync(playerId);
                if (pending > 0)
                    Logger.LogWarning($"Cola offline: {pending} evento(s) siguen pendientes tras el intento de flush.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Fallo al sincronizar cola offline: {ex}");
            }
        }

        /// <summary>
        /// Ciclo manual de prueba end-to-end: preview -> redeem -> aplicar efecto ->
        /// trackear expiración. Disparado con TestRedeemKey (default F6) mientras no
        /// exista un HUD real. Deja logs detallados en cada paso para depuración.
        /// </summary>
        private async Task TestRedeemPaddleSpeedBoostAsync(int playerId)
        {
            _isRedeemInFlight = true;
            try
            {
                var mechanic = _mechanics.Get(MmvPaddleSpeedBoost);
                if (mechanic is null)
                {
                    Logger.LogError($"mmv={MmvPaddleSpeedBoost} no está en el catálogo cacheado — ¿RefreshAsync corrió bien?");
                    return;
                }

                var request = new RedeemRequestDto
                {
                    ModifiableMechanicVideogameId = MmvPaddleSpeedBoost,
                    AttributeId = _testAttributeId.Value,
                    Amount = _testAmount.Value,
                };

                var preview = await _api.PreviewRedeemAsync(playerId, request);
                if (preview is null || !preview.CanRedeem)
                {
                    Logger.LogWarning($"No se puede canjear: saldo={preview?.CurrentBalance ?? -1}, requerido={_testAmount.Value}.");
                    return;
                }

                var result = await _api.RedeemAsync(playerId, request);
                Logger.LogInfo($"Redeem OK: ledger_id={result?.PointsLedgerId}, saldo restante={result?.ResultingBalance}.");

                var effectResult = _interpreter.Apply(mechanic);
                if (!effectResult.Success)
                {
                    Logger.LogError($"El efecto se debitó en LSG pero falló al aplicarse en el juego: {effectResult.Warning}");
                    return;
                }
                if (effectResult.Warning is not null)
                    Logger.LogWarning(effectResult.Warning);

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
                    Logger.LogInfo($"Efecto activo por {duration.TotalSeconds}s: {mechanic.Name}.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Fallo en el ciclo de canje de prueba: {ex}");
            }
            finally
            {
                _isRedeemInFlight = false;
            }
        }
    }
}
