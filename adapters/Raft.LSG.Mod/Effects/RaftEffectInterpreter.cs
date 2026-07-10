using LSG.SDK.Core.Mechanics;
using LSG.SDK.Core.Models;
using Newtonsoft.Json.Linq;

namespace RaftLsgMod.Effects
{
    /// <summary>
    /// Traduce el catálogo de mecánicas de Raft (game_id=71) a efectos reales.
    ///
    ///   mmv=66 Paddle Speed Boost -> Harmony patch con estado temporal
    ///          (ver PaddleForcePatch + PaddleSpeedBoostState). Limitación
    ///          host/cliente documentada en PaddleForcePatch.cs.
    ///
    ///   mmv=67 Loot Luck Boost -> Harmony patch sobre
    ///          SO_MysteryPackageLoot.GetRandomItemFromPossibles (ver
    ///          LootLuckPatch + LootLuckBoostState). Redefinido 2026-07-10:
    ///          GetRandomItemFromPossibles() elige uniforme al azar, sin
    ///          niveles de rareza que amplificar — el efecto real es una
    ///          GARANTÍA de ítem (fallback al pool completo) en vez de un
    ///          "más probabilidad de objetos raros" que el juego no soporta.
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
            switch (effect.Mechanic.MmvId)
            {
                case MmvPaddleSpeedBoost:
                    PaddleSpeedBoostState.Reset();
                    break;
                case MmvLootLuckBoost:
                    LootLuckBoostState.Reset();
                    break;
            }
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
            LootLuckBoostState.IsActive = true;
            // ExpiresAt lo fija el llamador vía TimedEffectTracker, igual que Paddle.
            return EffectApplicationResult.Ok();
        }

        private static float ReadFloat(MechanicDto mechanic, string key, float fallback)
        {
            var value = mechanic.Options?[key]?.Value<float?>();
            return value ?? fallback;
        }
    }
}
