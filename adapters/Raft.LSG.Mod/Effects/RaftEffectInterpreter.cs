using LSG.SDK.Core.Mechanics;
using LSG.SDK.Core.Models;

namespace Raft.LSG.Mod.Effects
{
    /// <summary>
    /// Traduce el catálogo de mecánicas de Raft (game_id=71) a efectos reales.
    ///
    ///   mmv=66 Paddle Speed Boost -> Harmony patch con estado temporal
    ///          (ver PaddleForcePatch + PaddleSpeedBoostState). Limitación
    ///          host/cliente documentada en PaddleForcePatch.cs.
    ///
    ///   mmv=67 Loot Luck Boost -> PENDIENTE. Reemplazó a "Debris Scanner"
    ///          (que no correspondía a ningún sistema real de Raft) el
    ///          2026-07-03. Aún no se localizó en dnSpy el punto exacto de
    ///          generación de loot (candidatos: SO_TreasureLootSettings /
    ///          SO_MysteryPackageLoot.GetRandomItemFromPossibles). Placeholder
    ///          no-op seguro: el canje se procesa igual en LSG, pero no aplica
    ///          ningún efecto real todavía — no bloquea v0.2.0 del resto.
    /// </summary>
    internal sealed class RaftEffectInterpreter : ITimedEffectInterpreter
    {
        private const int MmvPaddleSpeedBoost = 66;
        private const int MmvLootLuckBoost = 67;

        public bool CanApply(MechanicDto mechanic) =>
            mechanic.MmvId == MmvPaddleSpeedBoost || mechanic.MmvId == MmvLootLuckBoost;

        public EffectApplicationResult Apply(MechanicDto mechanic)
        {
            return mechanic.MmvId switch
            {
                MmvPaddleSpeedBoost => ApplyPaddleSpeedBoost(mechanic),
                MmvLootLuckBoost => ApplyLootLuckBoost(),
                _ => EffectApplicationResult.Failed($"Mecánica no soportada por RaftEffectInterpreter: mmv={mechanic.MmvId}"),
            };
        }

        public void Revert(TimedEffect effect)
        {
            if (effect.Mechanic.MmvId == MmvPaddleSpeedBoost)
            {
                PaddleSpeedBoostState.Reset();
            }
            // MmvLootLuckBoost: instantáneo (placeholder), no requiere revert.
        }

        private EffectApplicationResult ApplyPaddleSpeedBoost(MechanicDto mechanic)
        {
            var multiplier = ReadFloat(mechanic, "speed_multiplier", fallback: 1.2f);

            PaddleSpeedBoostState.IsActive = true;
            PaddleSpeedBoostState.Multiplier = multiplier;
            // ExpiresAt lo fija el llamador (Plugin.cs) vía TimedEffectTracker al
            // registrar el TimedEffect; aquí solo activamos el multiplicador.

            return EffectApplicationResult.OkWithWarning(
                "Efecto solo visible si el jugador local hostea la partida (ver PaddleForcePatch.cs).");
        }

        private static EffectApplicationResult ApplyLootLuckBoost()
        {
            // TODO: implementar cuando se localice el punto real de generación de
            // loot en dnSpy. No falla para no romper el flujo de redeem.
            return EffectApplicationResult.OkWithWarning(
                "Loot Luck Boost: placeholder no-op, pendiente de mecanismo real.");
        }

        private static float ReadFloat(MechanicDto mechanic, string key, float fallback)
        {
            if (mechanic.Options is null)
                return fallback;

            if (mechanic.Options.Value.TryGetProperty(key, out var prop) && prop.TryGetSingle(out var value))
                return value;

            return fallback;
        }
    }
}
