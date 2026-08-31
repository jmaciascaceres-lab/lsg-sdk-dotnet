using System;
using System.Threading.Tasks;
using LSG.SDK.Core.Api;
using LSG.SDK.Core.Auth;
using LSG.SDK.Core.Config;
using LSG.SDK.Core.Mechanics;
using LSG.SDK.Core.Models;
using LSG.SDK.Core.Offline;
using LSGTerrariaMod.Effects;
using Terraria.ModLoader;

namespace LSGTerrariaMod
{
	/// <summary>
	/// OnModLoad() y PostUpdateEverything() confirmados en la documentación
	/// oficial de Terraria.ModLoader.ModSystem (docs.tmodloader.net) - "This
	/// hook is called right after Mod.Load()" / "Called after the Network got
	/// updated, this is the last hook that happens in an update."
	///
	/// TODO (pendiente, no bloqueante): credenciales hardcodeadas acá en vez
	/// de un archivo de config como SMAPI - tModLoader tiene su propio
	/// Terraria.ModLoader.Config.ModConfig para menús en el juego, pero se
	/// evitó a propósito por el choque de nombres con nuestras propias
	/// clases y para no sumar otra API nueva a aprender en el primer intento.
	/// Reemplazar "TU_EMAIL"/"TU_PASSWORD" antes de compilar.
	/// </summary>
	public class LSGModSystem : ModSystem, IModHelperLog
	{
		// Confirmado: id_videogame = 8 (cluster TMODLOADER).
		private const int LsgGameId = 8;

		private const string LsgEmail = "TU_EMAIL";
		private const string LsgPassword = "TU_PASSWORD";
		private const int NativeBuffDurationSeconds = 60;
		private const int MovementSpeedDurationSeconds = 60;

		private LsgConfig _lsgConfig = null!;
		private LsgAuthClient _auth = null!;
		private LsgCoreApiClient _api = null!;
		private MechanicsCache _mechanics = null!;
		private OfflineQueue _offlineQueue = null!;
		private TimedEffectTracker _timedEffects = null!;
		private IDurationResolver _durationResolver = null!;
		private TerrariaEffectInterpreter _interpreter = null!;

		private int? _playerId;
		private DateTimeOffset _lastOfflineFlush = DateTimeOffset.UtcNow;

		public override void OnModLoad()
		{
			_lsgConfig = new LsgConfig { GameId = LsgGameId, PluginVersion = Mod.Version.ToString() };
			_auth = new LsgAuthClient(_lsgConfig);
			_api = new LsgCoreApiClient(_lsgConfig, _auth);
			_mechanics = new MechanicsCache(_api);
			_offlineQueue = new OfflineQueue(_api, _lsgConfig);
			_timedEffects = new TimedEffectTracker();
			_durationResolver = new PassthroughDurationResolver();
			_interpreter = new TerrariaEffectInterpreter(this, NativeBuffDurationSeconds, MovementSpeedDurationSeconds);

			_mechanics.OnPlaceholderOptionsDetected += m =>
				LogWarn($"Mecánica '{m.Name}' (mmv={m.MmvId}) sin options reales - revisar catálogo.");

			_timedEffects.OnExpired += effect =>
			{
				_interpreter.Revert(effect);
				LogInfo($"Efecto expirado: {effect.Mechanic.Name} (mmv={effect.Mechanic.MmvId})");
			};

			LogInfo($"{Mod.Name} v{Mod.Version} cargado.");

			if (LsgEmail != "TU_EMAIL" && LsgPassword != "TU_PASSWORD")
			{
				_ = LoginAndInitializeAsync(LsgEmail, LsgPassword);
			}
			else
			{
				LogWarn("Credenciales no configuradas todavía - edita LsgEmail/LsgPassword en LSGModSystem.cs.");
			}
		}

		public override void PostUpdateEverything()
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
				LogError($"Fallo en PostUpdateEverything: {ex}");
			}
		}

		private async Task LoginAndInitializeAsync(string email, string password)
		{
			try
			{
				var session = await _auth.LoginAsync(email, password);
				_playerId = session.Player.IdPlayers;
				LogInfo($"Login OK - player_id={_playerId}, roles=[{string.Join(",", session.Player.Roles)}]");

				await _mechanics.RefreshAsync();
				LogInfo($"Catálogo de mecánicas cargado: {_mechanics.All.Count} mecánica(s) para game_id={LsgGameId}.");
			}
			catch (Exception ex)
			{
				LogError($"Fallo en LoginAndInitializeAsync: {ex}");
			}
		}

		private async Task FlushOfflineQueueAsync(int playerId)
		{
			try
			{
				var pending = await _offlineQueue.FlushAsync(playerId);
				if (pending > 0)
					LogWarn($"Cola offline: {pending} evento(s) siguen pendientes.");
			}
			catch (Exception ex)
			{
				LogError($"Fallo al sincronizar cola offline: {ex}");
			}
		}

		/// <summary>
		/// Ciclo de canje real, llamado desde el comando de chat /lsgredeem
		/// (ver Commands/LsgRedeemCommand.cs). attribute_id/amount se leen
		/// DIRECTO del catálogo cacheado (mechanic.Options) para las ~180
		/// mecánicas con reward_id - no hace falta config por mecánica.
		/// </summary>
		public async Task RedeemMechanicAsync(int mmvId)
		{
			if (!_playerId.HasValue)
			{
				LogError("No hay sesión activa - revisa las credenciales en LSGModSystem.cs.");
				return;
			}

			try
			{
				var mechanic = _mechanics.Get(mmvId);
				if (mechanic is null)
				{
					LogError($"mmv={mmvId} no está en el catálogo cacheado.");
					return;
				}

				var attributeId = mechanic.Options?["attribute_id"]?.ToObject<int?>() ?? 1;
				var amount = mechanic.Options?["cost_amount"]?.ToObject<int?>() ?? 1;

				var request = new RedeemRequestDto
				{
					ModifiableMechanicVideogameId = mmvId,
					AttributeId = attributeId,
					Amount = amount,
				};

				var preview = await _api.PreviewRedeemAsync(_playerId.Value, request);
				if (preview is null || !preview.CanRedeem)
				{
					LogWarn($"Saldo insuficiente para {mechanic.Name}: {preview?.CurrentBalance ?? -1} < {amount}.");
					return;
				}

				var result = await _api.RedeemAsync(_playerId.Value, request);
				LogInfo($"Redeem OK ({mechanic.Name}): ledger_id={result?.PointsLedgerId}, saldo restante={result?.ResultingBalance}.");

				var effectResult = _interpreter.Apply(mechanic);
				if (!effectResult.Success)
				{
					LogError($"Efecto no aplicado ({mechanic.Name}): {effectResult.Warning}");
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
					LogInfo($"Efecto activo por {duration.TotalSeconds}s: {mechanic.Name}.");
				}
			}
			catch (Exception ex)
			{
				LogError($"Fallo en el ciclo de canje (mmv={mmvId}): {ex}");
			}
		}

		public void LogInfo(string message) => Mod.Logger.Info(message);
		public void LogWarn(string message) => Mod.Logger.Warn(message);
		public void LogError(string message) => Mod.Logger.Error(message);
	}
}
