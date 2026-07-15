using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
using ValheimLsgMod.Effects;
using UnityEngine;

namespace ValheimLsgMod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        private const string PluginGuid = "cl.usach.diinf.lsg.valheim";
        private const string PluginName = "LSG Valheim Adapter";
        private const string PluginVersion = "0.2.0";

        // Confirmado: id_videogame = 17 (db_lsg.videogame, cluster BEPINEX).
        private const int LsgGameId = 17;
        private const int MmvStaminaRegenBoost = 60;
        private const int MmvComfortBoost = 61;

        private LsgConfig _config = null!;
        private LsgAuthClient _auth = null!;
        private LsgCoreApiClient _api = null!;
        private MechanicsCache _mechanics = null!;
        private OfflineQueue _offlineQueue = null!;
        private TimedEffectTracker _timedEffects = null!;
        private IDurationResolver _durationResolver = null!;
        private ValheimEffectInterpreter _interpreter = null!;
        private Harmony _harmony = null!;

        private ConfigEntry<string> _lsgEmail = null!;
        private ConfigEntry<string> _lsgPassword = null!;
        private ConfigEntry<bool> _autoLoginOnStart = null!;
        private ConfigEntry<int> _testAttributeId = null!;
        private ConfigEntry<int> _testAmount = null!;
        private ConfigEntry<int> _comfortAttributeId = null!;
        private ConfigEntry<int> _comfortAmount = null!;

        private int? _playerId;
        private bool _isRedeemInFlight;
        private bool _isLoggingIn;
        private string? _lastError;
        private System.Threading.Timer? _maintenanceTimer;
        private DateTimeOffset _lastOfflineFlush = DateTimeOffset.UtcNow;
        private DateTimeOffset _lastBalanceRefresh = DateTimeOffset.MinValue;

        // ---------- Estado del HUD (OnGUI) ----------
        private string _hudEmail = string.Empty;
        private string _hudPassword = string.Empty;
        private List<PointsBalanceEntry>? _cachedBalance;
        private bool _onGuiHeartbeatLogged;
        private ConfigEntry<int> _hudX = null!;
        private ConfigEntry<int> _hudY = null!;
        private Rect _hudRect;

        private void Awake()
        {
            BindConfig();
            _hudRect = new Rect(_hudX.Value, _hudY.Value, 320, 220);

            _config = new LsgConfig { GameId = LsgGameId, PluginVersion = PluginVersion };
            _auth = new LsgAuthClient(_config);
            _api = new LsgCoreApiClient(_config, _auth);
            _mechanics = new MechanicsCache(_api);
            _offlineQueue = new OfflineQueue(_api, _config);
            _timedEffects = new TimedEffectTracker();
            _durationResolver = new PassthroughDurationResolver();
            _interpreter = new ValheimEffectInterpreter();

            _hudEmail = _lsgEmail.Value;
            _hudPassword = _lsgPassword.Value;

            _mechanics.OnPlaceholderOptionsDetected += m =>
                Logger.LogWarning($"Mecánica '{m.Name}' (mmv={m.MmvId}) sin options reales — revisar catálogo.");

            _timedEffects.OnExpired += effect =>
            {
                _interpreter.Revert(effect);
                Logger.LogInfo($"Efecto expirado y revertido: {effect.Mechanic.Name} (mmv={effect.Mechanic.MmvId})");
            };

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            Logger.LogInfo($"{PluginName} v{PluginVersion} cargado. Patch de Harmony aplicado.");

            // Mantenimiento periódico (Tick de efectos, flush offline, refresh de
            // saldo) vía System.Threading.Timer puro de .NET — NO depende de
            // Update()/Start() de Unity (ver nota histórica en README del SDK-core:
            // esos callbacks no se invocan en este entorno, causa aún sin aislar).
            _maintenanceTimer = new System.Threading.Timer(MaintenanceTick, null,
                TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));

            if (_autoLoginOnStart.Value && !string.IsNullOrWhiteSpace(_lsgEmail.Value) && !string.IsNullOrWhiteSpace(_lsgPassword.Value))
            {
                Logger.LogInfo("AutoLoginOnStart habilitado — iniciando sesión con credenciales de BepInEx/config...");
                _ = LoginAndInitializeAsync(_lsgEmail.Value, _lsgPassword.Value);
            }
            else
            {
                Logger.LogInfo("Esperando login desde el HUD (esquina superior izquierda de la pantalla).");
            }
        }

        private void BindConfig()
        {
            // ADVERTENCIA DE SEGURIDAD (aceptado para esta fase de pruebas manuales):
            // si se completa aquí, la contraseña queda en texto plano en
            // BepInEx/config/cl.usach.diinf.lsg.valheim.cfg. El HUD (OnGUI) permite
            // loguearse sin tocar este archivo; estos campos solo sirven como
            // atajo opcional para pruebas repetidas. NO usar cuenta de producción.
            _lsgEmail = Config.Bind("LSG Credentials", "Email", "",
                "Email de la cuenta LSG (opcional — atajo para AutoLoginOnStart). Cuenta de prueba, no producción.");
            _lsgPassword = Config.Bind("LSG Credentials", "Password", "",
                "Password en texto plano (opcional — atajo para AutoLoginOnStart). Se puede loguear desde el HUD en vez de esto.");
            _autoLoginOnStart = Config.Bind("LSG Credentials", "AutoLoginOnStart", true,
                "Si es true y Email/Password están completos, inicia sesión automáticamente al cargar el mod.");

            _testAttributeId = Config.Bind("LSG Test", "TestAttributeId", 2,
                "attribute_id a debitar en el canje (2 = FISICO_BASE, tentativo — confirmar contra /attributes).");
            _testAmount = Config.Bind("LSG Test", "TestAmount", 30,
                "Monto a canjear al presionar el botón de Stamina Regen Boost en el HUD.");
            _comfortAttributeId = Config.Bind("LSG Test", "ComfortAttributeId", 4,
                "attribute_id a debitar en el canje de Comfort Boost (4 = MENTAL_BASE, tentativo).");
            _comfortAmount = Config.Bind("LSG Test", "ComfortAmount", 25,
                "Monto a canjear al presionar el botón de Comfort Boost en el HUD.");

            _hudX = Config.Bind("LSG HUD", "HudX", 10,
                "Posición X (píxeles) de la esquina superior izquierda del HUD de LSG.");
            _hudY = Config.Bind("LSG HUD", "HudY", 160,
                "Posición Y (píxeles). Default 160 (no 10) para no solapar las ranuras 1-3 del hotbar de Valheim.");
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

                if (_playerId.HasValue && (DateTimeOffset.UtcNow - _lastBalanceRefresh).TotalSeconds >= 10)
                {
                    _lastBalanceRefresh = DateTimeOffset.UtcNow;
                    _ = RefreshBalanceAsync(_playerId.Value);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Fallo en MaintenanceTick: {ex}");
            }
        }

        /// <summary>
        /// Login interactivo real: invocado desde el botón "Login" del HUD (OnGUI)
        /// o, opcionalmente, de forma automática al arrancar si AutoLoginOnStart
        /// está habilitado. Reemplaza el flujo v0.3.x que SOLO leía credenciales
        /// del archivo de configuración.
        /// </summary>
        private async Task LoginAndInitializeAsync(string email, string password)
        {
            _isLoggingIn = true;
            _lastError = null;
            try
            {
                var session = await _auth.LoginAsync(email, password);
                _playerId = session.Player.IdPlayers;
                Logger.LogInfo($"Login OK — player_id={_playerId}, roles=[{string.Join(",", session.Player.Roles)}]");

                await _mechanics.RefreshAsync();
                Logger.LogInfo($"Catálogo de mecánicas cargado: {_mechanics.All.Count} mecánica(s) para game_id={LsgGameId}.");

                await RefreshBalanceAsync(_playerId.Value);
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                Logger.LogError($"Fallo en LoginAndInitializeAsync: {ex}");
            }
            finally
            {
                _isLoggingIn = false;
            }
        }

        private async Task RefreshBalanceAsync(int playerId)
        {
            try
            {
                _cachedBalance = await _api.GetPointsBalanceAsync(playerId);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Fallo al refrescar saldo: {ex}");
            }
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

        // ---------- HUD (OnGUI) ----------
        // NOTA: OnGUI es OTRO callback de Unity por frame (igual que Update()/Start(),
        // que no se invocan en este entorno por una causa aún sin aislar). Se agrega
        // el mismo patrón de heartbeat + try/catch para diagnosticar de inmediato si
        // OnGUI tampoco corre, en vez de descubrirlo después de construir todo el HUD.

        private void OnGUI()
        {
            try
            {
                if (!_onGuiHeartbeatLogged)
                {
                    _onGuiHeartbeatLogged = true;
                    Logger.LogInfo("OnGUI() ejecutándose (heartbeat) — el HUD debería ser visible en pantalla.");
                }

                GUI.Box(_hudRect, "LSG - Valheim Adapter");
                GUILayout.BeginArea(new Rect(_hudRect.x + 10, _hudRect.y + 25, _hudRect.width - 20, _hudRect.height - 35));

                if (!_playerId.HasValue)
                {
                    DrawLoginForm();
                }
                else
                {
                    DrawBalanceAndRedeemPanel();
                }

                GUILayout.EndArea();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Excepción en OnGUI(): {ex}");
            }
        }

        private void DrawLoginForm()
        {
            GUILayout.Label("Email:");
            _hudEmail = GUILayout.TextField(_hudEmail);
            GUILayout.Label("Password:");
            _hudPassword = GUILayout.PasswordField(_hudPassword, '*');

            GUI.enabled = !_isLoggingIn;
            if (GUILayout.Button(_isLoggingIn ? "Conectando..." : "Login"))
            {
                _ = LoginAndInitializeAsync(_hudEmail, _hudPassword);
            }
            GUI.enabled = true;

            if (_lastError is not null)
                GUILayout.Label($"Error: {_lastError}");
        }

        private void DrawBalanceAndRedeemPanel()
        {
            GUILayout.Label($"Jugador #{_playerId}");

            var fisico = _cachedBalance?.FirstOrDefault(b => b.AttributeId == _testAttributeId.Value);
            var mental = _cachedBalance?.FirstOrDefault(b => b.AttributeId == _comfortAttributeId.Value);
            GUILayout.Label(fisico is not null
                ? $"Saldo físico (attr={_testAttributeId.Value}): {fisico.Balance} pts"
                : "Saldo físico: cargando...");
            GUILayout.Label(mental is not null
                ? $"Saldo mental (attr={_comfortAttributeId.Value}): {mental.Balance} pts"
                : "Saldo mental: cargando...");

            GUI.enabled = !_isRedeemInFlight;
            if (GUILayout.Button($"Canjear Stamina Regen Boost ({_testAmount.Value} pts)"))
            {
                _ = RedeemMechanicAsync(_playerId!.Value, MmvStaminaRegenBoost, _testAttributeId.Value, _testAmount.Value);
            }
            if (GUILayout.Button($"Canjear Comfort Boost ({_comfortAmount.Value} pts)"))
            {
                _ = RedeemMechanicAsync(_playerId!.Value, MmvComfortBoost, _comfortAttributeId.Value, _comfortAmount.Value);
            }
            GUI.enabled = true;

            foreach (var active in _timedEffects.GetActive())
            {
                var remaining = (active.ExpiresAt - DateTimeOffset.UtcNow).TotalSeconds;
                GUILayout.Label(remaining > 0
                    ? $"{active.Mechanic.Name}: {remaining:F0}s restantes"
                    : $"{active.Mechanic.Name}: expirando...");
            }

            if (_lastError is not null)
                GUILayout.Label($"Error: {_lastError}");
        }

        /// <summary>
        /// Ciclo de canje real, genérico para cualquier mecánica del catálogo:
        /// preview -> redeem -> aplicar efecto -> trackear expiración. Invocado
        /// desde los botones del HUD.
        /// </summary>
        private async Task RedeemMechanicAsync(int playerId, int mmvId, int attributeId, int amount)
        {
            _isRedeemInFlight = true;
            _lastError = null;
            try
            {
                var mechanic = _mechanics.Get(mmvId);
                if (mechanic is null)
                {
                    _lastError = $"mmv={mmvId} no está en el catálogo cacheado.";
                    Logger.LogError(_lastError);
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
                    _lastError = $"Saldo insuficiente: {preview?.CurrentBalance ?? -1} < {amount}.";
                    Logger.LogWarning(_lastError);
                    return;
                }

                var result = await _api.RedeemAsync(playerId, request);
                Logger.LogInfo($"Redeem OK ({mechanic.Name}): ledger_id={result?.PointsLedgerId}, saldo restante={result?.ResultingBalance}.");

                var effectResult = _interpreter.Apply(mechanic);
                if (!effectResult.Success)
                {
                    _lastError = $"Efecto no aplicado: {effectResult.Warning}";
                    Logger.LogError(_lastError);
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
                    Logger.LogInfo($"Efecto activo por {duration.TotalSeconds}s: {mechanic.Name}.");
                }

                await RefreshBalanceAsync(playerId);
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                Logger.LogError($"Fallo en el ciclo de canje: {ex}");
            }
            finally
            {
                _isRedeemInFlight = false;
            }
        }
    }
}
