using System;
using System.Threading.Tasks;
using LSG.SDK.Core.Api;
using LSG.SDK.Core.Auth;
using LSG.SDK.Core.Config;
using LSG.SDK.Core.Mechanics;
using LSG.SDK.Core.Models;
using LSG.SDK.Core.Offline;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValleyLsgMod.Effects;

namespace StardewValleyLsgMod
{
    /// <summary>
    /// SMAPI es notablemente más simple que BepInEx/IL2CPP (VRising):
    ///   - Un solo proceso (sin arquitectura cliente/servidor separada).
    ///   - .NET normal, sin capa de interop IL2CPP.
    ///   - Sistema de comandos de consola integrado (helper.ConsoleCommands)
    ///     para probar sin construir un HUD propio.
    /// </summary>
    public sealed class ModEntry : Mod
    {
        // Confirmado: id_videogame = 12 (cluster SMAPI).
        private const int LsgGameId = 12;

        private ModConfig _config = null!;
        private LsgConfig _lsgConfig = null!;
        private LsgAuthClient _auth = null!;
        private LsgCoreApiClient _api = null!;
        private MechanicsCache _mechanics = null!;
        private OfflineQueue _offlineQueue = null!;
        private TimedEffectTracker _timedEffects = null!;
        private IDurationResolver _durationResolver = null!;
        private StardewEffectInterpreter _interpreter = null!;

        private int? _playerId;
        private DateTimeOffset _lastOfflineFlush = DateTimeOffset.UtcNow;

        public override void Entry(IModHelper helper)
        {
            _config = helper.ReadConfig<ModConfig>();

            _lsgConfig = new LsgConfig { GameId = LsgGameId, PluginVersion = ModManifest.Version.ToString() };
            _auth = new LsgAuthClient(_lsgConfig);
            _api = new LsgCoreApiClient(_lsgConfig, _auth);
            _mechanics = new MechanicsCache(_api);
            _offlineQueue = new OfflineQueue(_api, _lsgConfig);
            _timedEffects = new TimedEffectTracker();
            _durationResolver = new PassthroughDurationResolver();
            _interpreter = new StardewEffectInterpreter(
                Monitor,
                _config.SpeedBuffAmount,
                _config.SpeedBuffDurationSeconds,
                _config.MiningXpAmount);

            _mechanics.OnPlaceholderOptionsDetected += m =>
                Monitor.Log($"Mecánica '{m.Name}' (mmv={m.MmvId}) sin options reales - revisar catálogo.", LogLevel.Warn);

            _timedEffects.OnExpired += effect =>
            {
                _interpreter.Revert(effect);
                Monitor.Log($"Efecto expirado: {effect.Mechanic.Name} (mmv={effect.Mechanic.MmvId})", LogLevel.Info);
            };

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondUpdateTicked;

            // Comandos de consola para probar cada mecánica confirmada (sin
            // HUD todavía - equivalente al "AutoTestRedeemOnLogin" de
            // VRising, pero disparado a mano).
            helper.ConsoleCommands.Add(
                "lsg_speed",
                "Canjea Speed Buff (mmv=77). Uso: lsg_speed",
                (_, _) => _ = RedeemMechanicAsync(
                    StardewEffectInterpreter.MmvSpeedBuff,
                    _config.SpeedBuffAttributeId,
                    _config.SpeedBuffCostAmount));

            helper.ConsoleCommands.Add(
                "lsg_mining",
                "Canjea Mining XP (mmv=84). Uso: lsg_mining",
                (_, _) => _ = RedeemMechanicAsync(
                    StardewEffectInterpreter.MmvMiningXp,
                    _config.MiningXpAttributeId,
                    _config.MiningXpCostAmount));

            Monitor.Log($"{ModManifest.Name} v{ModManifest.Version} cargado.", LogLevel.Info);
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            if (_config.AutoLoginOnStart && !string.IsNullOrWhiteSpace(_config.Email) && !string.IsNullOrWhiteSpace(_config.Password))
            {
                Monitor.Log("AutoLoginOnStart habilitado - iniciando sesión...", LogLevel.Info);
                _ = LoginAndInitializeAsync(_config.Email, _config.Password);
            }
            else
            {
                Monitor.Log("Sin credenciales en config.json - completa Email/Password para loguear.", LogLevel.Info);
            }
        }

        private void OnOneSecondUpdateTicked(object? sender, OneSecondUpdateTickedEventArgs e)
        {
            try
            {
                _timedEffects.Tick();

                if (_playerId.HasValue &&
                    (DateTimeOffset.UtcNow - _lastOfflineFlush).TotalSeconds >= _lsgConfig.OfflineFlushIntervalSeconds)
                {
                    _lastOfflineFlush = DateTimeOffset.UtcNow;
                    _ = FlushOfflineQueueAsync(_playerId.Value);
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"Fallo en OnOneSecondUpdateTicked: {ex}", LogLevel.Error);
            }
        }

        private async Task LoginAndInitializeAsync(string email, string password)
        {
            try
            {
                var session = await _auth.LoginAsync(email, password);
                _playerId = session.Player.IdPlayers;
                Monitor.Log($"Login OK - player_id={_playerId}, roles=[{string.Join(",", session.Player.Roles)}]", LogLevel.Info);

                await _mechanics.RefreshAsync();
                Monitor.Log($"Catálogo de mecánicas cargado: {_mechanics.All.Count} mecánica(s) para game_id={LsgGameId}.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Fallo en LoginAndInitializeAsync: {ex}", LogLevel.Error);
            }
        }

        private async Task FlushOfflineQueueAsync(int playerId)
        {
            try
            {
                var pending = await _offlineQueue.FlushAsync(playerId);
                if (pending > 0)
                    Monitor.Log($"Cola offline: {pending} evento(s) siguen pendientes.", LogLevel.Warn);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Fallo al sincronizar cola offline: {ex}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Ciclo de canje real: preview -> redeem -> aplicar efecto -> trackear
        /// expiración (si aplica). Mismo patrón que Raft/Valheim/VRising.
        /// </summary>
        private async Task RedeemMechanicAsync(int mmvId, int attributeId, int amount)
        {
            if (!_playerId.HasValue)
            {
                Monitor.Log("No hay sesión activa - revisa Email/Password en config.json.", LogLevel.Error);
                return;
            }

            try
            {
                var mechanic = _mechanics.Get(mmvId);
                if (mechanic is null)
                {
                    Monitor.Log($"mmv={mmvId} no está en el catálogo cacheado.", LogLevel.Error);
                    return;
                }

                var request = new RedeemRequestDto
                {
                    ModifiableMechanicVideogameId = mmvId,
                    AttributeId = attributeId,
                    Amount = amount,
                };

                var preview = await _api.PreviewRedeemAsync(_playerId.Value, request);
                if (preview is null || !preview.CanRedeem)
                {
                    Monitor.Log($"Saldo insuficiente para {mechanic.Name}: {preview?.CurrentBalance ?? -1} < {amount}.", LogLevel.Warn);
                    return;
                }

                var result = await _api.RedeemAsync(_playerId.Value, request);
                Monitor.Log($"Redeem OK ({mechanic.Name}): ledger_id={result?.PointsLedgerId}, saldo restante={result?.ResultingBalance}.", LogLevel.Info);

                var effectResult = _interpreter.Apply(mechanic);
                if (!effectResult.Success)
                {
                    Monitor.Log($"Efecto no aplicado ({mechanic.Name}): {effectResult.Warning}", LogLevel.Error);
                    return;
                }

                var duration = _durationResolver.Resolve(mechanic, new EffectContext { PlayerId = _playerId.Value });
                if (duration > TimeSpan.Zero)
                {
                    _timedEffects.Track(new TimedEffect
                    {
                        PlayerId = _playerId.Value,
                        Mechanic = mechanic,
                        ExpiresAt = DateTimeOffset.UtcNow + duration,
                        RevertState = effectResult.RevertState,
                    });
                    Monitor.Log($"Efecto activo por {duration.TotalSeconds}s: {mechanic.Name}.", LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"Fallo en el ciclo de canje (mmv={mmvId}): {ex}", LogLevel.Error);
            }
        }
    }
}
