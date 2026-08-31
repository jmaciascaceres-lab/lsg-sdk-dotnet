using LSG.SDK.Core.Mechanics;
using LSG.SDK.Core.Models;
using Newtonsoft.Json.Linq;
using Terraria;
using Terraria.ModLoader;

namespace LSGTerrariaMod.Effects
{
	/// <summary>
	/// Confirmado en el código fuente oficial de tModLoader (público en
	/// GitHub, sin necesitar dnSpy):
	///
	///   Vía GENÉRICA (cubre ~180 de las 211 mecánicas del catálogo real,
	///   todas las que traen "reward_id" en options): Player.AddBuff(int
	///   buffType, int time, bool quiet = true). Los reward_id del catálogo
	///   son los IDs nativos de BuffID de Terraria (mascotas, monturas,
	///   pociones, etc.) - un solo código cubre todas, no hace falta
	///   implementar cada una por separado.
	///
	///   Vía CUSTOM (Player Movement Speed, mmv=16): no existe un setter
	///   directo seguro para la velocidad - se aplica cada tick vía
	///   ModPlayer.PostUpdateRunSpeeds() (ver LSGPlayer.cs), multiplicando
	///   maxRunSpeed/runAcceleration. Documentado explícitamente para este
	///   propósito en el código fuente de ModPlayer.cs.
	///
	/// Ninguna de las dos necesita Harmony.
	///
	/// El catálogo real de LSG (game_id=8) SÍ trae cost_amount/attribute_id
	/// en options para las mecánicas con reward_id (a diferencia de
	/// Stardew Valley) - el ciclo de canje los lee directo del catálogo
	/// cacheado, sin necesitar config por mecánica.
	/// </summary>
	public sealed class TerrariaEffectInterpreter : ITimedEffectInterpreter
	{
		public const int MmvMovementSpeed = 16;

		private readonly IModHelperLog _log;
		private readonly int _nativeBuffDurationTicks;
		private readonly int _movementSpeedDurationTicks;

		public TerrariaEffectInterpreter(IModHelperLog log, int nativeBuffDurationSeconds, int movementSpeedDurationSeconds)
		{
			_log = log;
			// Terraria corre a 60 ticks/seg - los tiempos de buff se expresan
			// en ticks, no segundos, en toda la API (AddBuff, buffTime, etc.)
			_nativeBuffDurationTicks = nativeBuffDurationSeconds * 60;
			_movementSpeedDurationTicks = movementSpeedDurationSeconds * 60;
		}

		public bool CanApply(MechanicDto mechanic) =>
			mechanic.Options?["reward_id"] is not null || mechanic.MmvId == MmvMovementSpeed;

		public EffectApplicationResult Apply(MechanicDto mechanic)
		{
			var player = Main.LocalPlayer;
			if (player is null || !player.active)
			{
				return EffectApplicationResult.Failed("Main.LocalPlayer no existe todavía (¿sin partida cargada?).");
			}

			var rewardId = mechanic.Options?["reward_id"]?.ToObject<int?>();
			if (rewardId.HasValue)
			{
				player.AddBuff(rewardId.Value, _nativeBuffDurationTicks, quiet: false);
				_log.LogInfo($"Buff nativo aplicado ({mechanic.Name}): AddBuff({rewardId.Value}, {_nativeBuffDurationTicks} ticks).");
				return EffectApplicationResult.Ok();
			}

			if (mechanic.MmvId == MmvMovementSpeed)
			{
				var multiplier = mechanic.Options?["speed_multiplier"]?.ToObject<float?>() ?? 1.3f;
				var lsgPlayer = player.GetModPlayer<LSGPlayer>();
				var baselineSpeed = player.maxRunSpeed;
				lsgPlayer.LsgSpeedBuffActive = true;
				lsgPlayer.LsgSpeedMultiplier = multiplier;
				lsgPlayer.RequestDiagnosticLog(baselineSpeed);
				_log.LogInfo($"Player Movement Speed aplicado: maxRunSpeed base={baselineSpeed:F2}, x{multiplier} por {_movementSpeedDurationTicks / 60}s.");
				return EffectApplicationResult.Ok();
			}

			return EffectApplicationResult.Failed($"Mecánica no soportada por TerrariaEffectInterpreter: mmv={mechanic.MmvId}");
		}

		public void Revert(TimedEffect effect)
		{
			// Vía genérica (AddBuff): el propio sistema de buffs de Terraria
			// maneja la expiración solo (buffTime llega a 0) - no hace falta
			// Revert() manual.
			//
			// Player Movement Speed: SÍ necesita Revert() manual, ya que
			// LsgSpeedBuffActive es estado nuestro, no un buff nativo del juego.
			if (effect.Mechanic.MmvId == MmvMovementSpeed)
			{
				var player = Main.LocalPlayer;
				if (player is not null)
				{
					player.GetModPlayer<LSGPlayer>().LsgSpeedBuffActive = false;
				}
			}
		}
	}

	/// <summary>
	/// Abstracción mínima de logging para no atar TerrariaEffectInterpreter
	/// directamente a Mod.Logger (facilita testing y desacopla del ciclo de
	/// vida del Mod). LSGModSystem la implementa delegando a Mod.Logger.
	/// </summary>
	public interface IModHelperLog
	{
		void LogInfo(string message);
		void LogWarn(string message);
		void LogError(string message);
	}
}
