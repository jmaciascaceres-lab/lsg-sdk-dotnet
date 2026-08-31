using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace LSGTerrariaMod.Commands
{
	/// <summary>
	/// Uso en el chat del juego: /lsgredeem <mmv_id>
	/// Ej: /lsgredeem 16 (Player Movement Speed), /lsgredeem 90 (Araña Buff).
	///
	/// NOTA: a diferencia de Player.AddBuff, ModPlayer.PostUpdateRunSpeeds,
	/// ModSystem.OnModLoad y ModSystem.PostUpdateEverything (los 4
	/// confirmados contra el código fuente/documentación oficial esta
	/// sesión), la API exacta de ModCommand no se re-verificó de la misma
	/// forma - es una API estable y muy usada en la comunidad, pero si algo
	/// no compila acá, es el primer lugar a revisar.
	/// </summary>
	public class LsgRedeemCommand : ModCommand
	{
		public override CommandType Type => CommandType.Chat;
		public override string Command => "lsgredeem";
		public override string Usage => "/lsgredeem <mmv_id>";
		public override string Description => "Canjea una mecánica LSG por su mmv_id (id_modifiable_mechanic_videogame).";

		public override void Action(CommandCaller caller, string input, string[] args)
		{
			if (args.Length < 1 || !int.TryParse(args[0], out var mmvId))
			{
				caller.Reply("Uso: /lsgredeem <mmv_id>", Color.Red);
				return;
			}

			var system = ModContent.GetInstance<LSGModSystem>();
			_ = system.RedeemMechanicAsync(mmvId);
			caller.Reply($"Canjeando mmv={mmvId}... (ver consola de tModLoader para el resultado)");
		}
	}
}
