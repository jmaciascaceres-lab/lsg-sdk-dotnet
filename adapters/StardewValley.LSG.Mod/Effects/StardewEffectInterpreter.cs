using LSG.SDK.Core.Mechanics;
using LSG.SDK.Core.Models;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buffs;

namespace StardewValleyLsgMod.Effects
{
    /// <summary>
    /// Confirmado en dnSpy (Stardew Valley.dll 1.6.15.24356):
    ///
    ///   Speed Buff (mmv=77) -> Farmer.addedSpeed tiene setter [Obsolete] vacío
    ///     ("Player speed can't be changed directly... via applyBuff instead").
    ///     Camino real: construir un StardewValley.Buff con
    ///     BuffEffects.Speed seteado, y aplicarlo vía Farmer.applyBuff(Buff).
    ///     El juego maneja la expiración solo (Buff.millisecondsDuration) -
    ///     no hace falta Revert() manual, mismo patrón que Movement Speed
    ///     Boost en VRising (LifeTime del propio buff).
    ///
    ///   Mining XP (mmv=84) -> Farmer.gainExperience(int which, int howMuch)
    ///     con which=3 (confirmado por el switch interno: 0=Farming,
    ///     1=Fishing, 2=Foraging, 3=Mining, 4=Combat, 5=Luck -bloqueado en
    ///     este método-). Dispara el flujo real del juego (subida de nivel,
    ///     mensajes), no un simple contador.
    ///
    /// El catálogo real de LSG (game_id=12) NO trae duration_seconds ni
    /// magnitud en `options` para estas mecánicas (a diferencia de
    /// Raft/Valheim/VRising) - por eso van hardcodeados/configurables acá,
    /// no leídos de mechanic.Options.
    /// </summary>
    internal sealed class StardewEffectInterpreter : ITimedEffectInterpreter
    {
        public const int MmvSpeedBuff = 77;
        public const int MmvMiningXp = 84;

        private const string LsgSpeedBuffId = "lsg_speed_buff";

        private readonly IMonitor _monitor;
        private readonly float _speedAmount;
        private readonly int _speedDurationMs;
        private readonly int _miningXpAmount;

        public StardewEffectInterpreter(IMonitor monitor, float speedAmount, int speedDurationSeconds, int miningXpAmount)
        {
            _monitor = monitor;
            _speedAmount = speedAmount;
            _speedDurationMs = speedDurationSeconds * 1000;
            _miningXpAmount = miningXpAmount;
        }

        public bool CanApply(MechanicDto mechanic) =>
            mechanic.MmvId == MmvSpeedBuff || mechanic.MmvId == MmvMiningXp;

        public EffectApplicationResult Apply(MechanicDto mechanic)
        {
            return mechanic.MmvId switch
            {
                MmvSpeedBuff => ApplySpeedBuff(),
                MmvMiningXp => ApplyMiningXp(),
                _ => EffectApplicationResult.Failed($"Mecánica no soportada por StardewEffectInterpreter: mmv={mechanic.MmvId}"),
            };
        }

        public void Revert(TimedEffect effect)
        {
            // Speed Buff: el propio StardewValley.Buff se remueve solo al
            // llegar a 0 en millisecondsDuration (mismo patrón que el
            // LifeTime de VRising) - no hay estado propio que revertir acá.
            // Mining XP: instantáneo, nunca se trackea como TimedEffect.
        }

        private EffectApplicationResult ApplySpeedBuff()
        {
            if (Game1.player is null)
            {
                return EffectApplicationResult.Failed("Game1.player es null (¿partida no cargada todavía?).");
            }

            var effects = new BuffEffects();
            effects.Speed.Value = _speedAmount;

            var buff = new Buff(
                id: LsgSpeedBuffId,
                source: "LifeSync-Games",
                displaySource: "LifeSync-Games",
                duration: _speedDurationMs,
                effects: effects,
                displayName: "LSG Speed Buff",
                description: "Boost de velocidad otorgado por LifeSync-Games.");

            Game1.player.applyBuff(buff);
            _monitor.Log($"Speed Buff aplicado: +{_speedAmount} velocidad por {_speedDurationMs / 1000}s.", LogLevel.Info);
            return EffectApplicationResult.Ok();
        }

        private EffectApplicationResult ApplyMiningXp()
        {
            if (Game1.player is null)
            {
                return EffectApplicationResult.Failed("Game1.player es null (¿partida no cargada todavía?).");
            }

            var before = Game1.player.experiencePoints[3];
            Game1.player.gainExperience(3, _miningXpAmount);
            var after = Game1.player.experiencePoints[3];

            _monitor.Log($"Mining XP aplicado: {before} -> {after} (+{_miningXpAmount}).", LogLevel.Info);
            return EffectApplicationResult.Ok();
        }
    }
}
