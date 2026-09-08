using LSG.SDK.Core.Mechanics;
using LSG.SDK.Core.Models;
using Newtonsoft.Json.Linq;

namespace CitiesSkylinesLsgMod.Effects
{
    /// <summary>
    /// Confirmado en la documentación oficial de ICities (sin BepInEx, sin
    /// Harmony - API de primera parte del propio juego):
    ///
    ///   Cash Income Bonus (mmv=5) -> EconomyExtensionBase.OnUpdateMoneyAmount,
    ///     aplicado una sola vez vía EconomyExtension.PendingBonus.
    ///
    ///   Milestone Unlock Boost (mmv=?, PENDIENTE crear en el catálogo real
    ///     de LSG - ver patch SQL en SETUP.md) -> IMilestones.UnlockMilestone(name),
    ///     vía MilestonesExtension.Manager.
    ///
    /// LIMITACIÓN v0.1 en Milestone Unlock: no se confirmó un método para
    /// verificar qué milestones ya están desbloqueados (solo EnumerateMilestones()
    /// + UnlockMilestone(name), sin "IsUnlocked" confirmado) - por ahora
    /// desbloquea el PRIMERO que devuelva EnumerateMilestones(), sin filtrar.
    /// Pendiente de refinar una vez que se pueda probar en juego real y ver
    /// qué strings/orden devuelve efectivamente.
    /// </summary>
    internal sealed class CitiesSkylinesEffectInterpreter : ITimedEffectInterpreter
    {
        public const int MmvCashIncomeBonus = 5;

        // Confirmado: id_modifiable_mechanic_videogame = 214 (¡no 209! - ese
        // es id_modifiable_mechanic, el ID genérico compartido entre juegos,
        // no el específico de Cities: Skylines que usamos como "mmv" en todo
        // el proyecto).
        public const int MmvMilestoneUnlock = 214;

        public bool CanApply(MechanicDto mechanic) =>
            mechanic.MmvId == MmvCashIncomeBonus || mechanic.MmvId == MmvMilestoneUnlock;

        public EffectApplicationResult Apply(MechanicDto mechanic)
        {
            return mechanic.MmvId switch
            {
                MmvCashIncomeBonus => ApplyCashIncomeBonus(mechanic),
                MmvMilestoneUnlock => ApplyMilestoneUnlock(),
                _ => EffectApplicationResult.Failed($"Mecánica no soportada por CitiesSkylinesEffectInterpreter: mmv={mechanic.MmvId}"),
            };
        }

        public void Revert(TimedEffect effect)
        {
            // Cash Income Bonus: instantáneo, no se trackea como TimedEffect.
            // Milestone Unlock: instantáneo e irreversible por diseño (no
            // tiene sentido "re-bloquear" un milestone).
        }

        private EffectApplicationResult ApplyCashIncomeBonus(MechanicDto mechanic)
        {
            // El catálogo real trae options={"Cash income bonus": "..."} -
            // no un monto numérico. Fallback razonable documentado como
            // supuesto explícito mientras el catálogo no traiga un valor real.
            var amount = mechanic.Options?["amount"]?.ToObject<long?>() ?? 10000L;
            EconomyExtension.PendingBonus += amount;
            return EffectApplicationResult.Ok();
        }

        private EffectApplicationResult ApplyMilestoneUnlock()
        {
            var manager = MilestonesExtension.Manager;
            if (manager is null)
            {
                return EffectApplicationResult.Failed("MilestonesExtension.Manager es null (¿mod no completamente inicializado?).");
            }

            var milestones = manager.EnumerateMilestones();
            if (milestones is null || milestones.Length == 0)
            {
                return EffectApplicationResult.Failed("EnumerateMilestones() no devolvió ningún milestone.");
            }

            var target = milestones[0];
            manager.UnlockMilestone(target);
            UnityEngine.Debug.Log($"[LSG] Milestone Unlock Boost aplicado: '{target}' desbloqueado (primero de {milestones.Length} encontrados - sin filtrar por ya-desbloqueados, ver limitación v0.1).");
            return EffectApplicationResult.Ok();
        }
    }
}
