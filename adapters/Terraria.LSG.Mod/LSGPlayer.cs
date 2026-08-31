using Terraria.ModLoader;

namespace LSGTerrariaMod
{
	/// <summary>
	/// PostUpdateRunSpeeds() confirmado en el código fuente oficial de
	/// tModLoader ("Use this to modify maxRunSpeed, accRunSpeed,
	/// runAcceleration..."). La propiedad heredada de ModPlayer para acceder
	/// al Player es "Player" (mayúscula) - confirmado por el COMPILADOR real
	/// de la versión instalada (1.4.4.9+2026.06.3.6), que contradijo el
	/// "player" minúscula que había leído en el código fuente de GitHub (esa
	/// build parece corresponder a una rama/versión ligeramente distinta -
	/// el compilador real manda sobre la documentación cuando difieren).
	///
	/// Mismo patrón que Raft/Valheim ("*State" + hook por tick), pero acá
	/// usamos el hook nativo de tModLoader en vez de Harmony - Terraria no
	/// necesita Harmony para ninguna de nuestras dos mecánicas.
	/// </summary>
	public class LSGPlayer : ModPlayer
	{
		public bool LsgSpeedBuffActive;
		public float LsgSpeedMultiplier = 1f;

		// Diagnóstico: log de "antes/después" en el primer tick tras
		// activarse, en vez de cada tick (60/seg inundaría la consola).
		private bool _pendingDiagnosticLog;
		private float _diagnosticBaseline;

		public void RequestDiagnosticLog(float baselineMaxRunSpeed)
		{
			_pendingDiagnosticLog = true;
			_diagnosticBaseline = baselineMaxRunSpeed;
		}

		public override void PostUpdateRunSpeeds()
		{
			if (LsgSpeedBuffActive)
			{
				Player.maxRunSpeed *= LsgSpeedMultiplier;
				Player.runAcceleration *= LsgSpeedMultiplier;

				if (_pendingDiagnosticLog)
				{
					_pendingDiagnosticLog = false;
					Mod.Logger.Info($"[LSG] Speed Buff confirmado en juego: maxRunSpeed {_diagnosticBaseline:F2} -> {Player.maxRunSpeed:F2} (x{LsgSpeedMultiplier}).");
				}
			}
		}
	}
}
