using BepInEx.Logging;
using LSG.SDK.Core.Mechanics;
using LSG.SDK.Core.Models;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ValheimLsgMod.Effects
{
    /// <summary>
    /// Traduce el catálogo de mecánicas de Valheim (game_id=17) a efectos reales.
    /// Confirmado en dnSpy sobre assembly_valheim.dll (2026-07-10):
    ///
    ///   mmv=60 Stamina Regen Boost -> SISTEMA NATIVO, sin Harmony. Se crea una
    ///          instancia de SE_Stats en runtime (ScriptableObject.CreateInstance)
    ///          con m_staminaRegenMultiplier y m_ttl, y se agrega vía
    ///          Player.GetSEMan().AddStatusEffect(...). Valheim maneja la
    ///          expiración él solo (incluso muestra su propio ícono de buff).
    ///
    ///   mmv=61 Comfort Boost -> requiere Harmony (ver ComfortBoostPatch) porque
    ///          el comfort se recalcula desde cero cada ~2s en base a piezas
    ///          cercanas, no es un valor acumulable como stamina.
    /// </summary>
    internal sealed class ValheimEffectInterpreter : ITimedEffectInterpreter
    {
        private const int MmvStaminaRegenBoost = 60;
        private const int MmvComfortBoost = 61;

        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("ValheimLsgMod.ValheimEffectInterpreter");

        public bool CanApply(MechanicDto mechanic) =>
            mechanic.MmvId == MmvStaminaRegenBoost || mechanic.MmvId == MmvComfortBoost;

        public EffectApplicationResult Apply(MechanicDto mechanic)
        {
            return mechanic.MmvId switch
            {
                MmvStaminaRegenBoost => ApplyStaminaRegenBoost(mechanic),
                MmvComfortBoost => ApplyComfortBoost(mechanic),
                _ => EffectApplicationResult.Failed($"Mecánica no soportada por ValheimEffectInterpreter: mmv={mechanic.MmvId}"),
            };
        }

        public void Revert(TimedEffect effect)
        {
            switch (effect.Mechanic.MmvId)
            {
                case MmvStaminaRegenBoost:
                    // No-op: el SE_Stats creado en Apply() tiene su propio m_ttl y
                    // Valheim lo remueve solo. Nuestro TimedEffectTracker igual lo
                    // trackea para mostrar el countdown en NUESTRO HUD, pero no hay
                    // nada que revertir manualmente de este lado.
                    break;
                case MmvComfortBoost:
                    ComfortBoostState.Reset();
                    break;
            }
        }

        private EffectApplicationResult ApplyStaminaRegenBoost(MechanicDto mechanic)
        {
            var player = Player.m_localPlayer;
            if (player is null)
            {
                return EffectApplicationResult.Failed("No hay Player.m_localPlayer activo — ¿estás en una partida cargada?");
            }

            var multiplier = ReadFloat(mechanic, "regen_multiplier", fallback: 1.3f);
            var durationSeconds = ReadFloat(mechanic, "duration_seconds", fallback: 900f);

            var se = ScriptableObject.CreateInstance<SE_Stats>();
            se.name = "LSG_StaminaRegenBoost";
            se.m_name = "LSG Stamina Regen Boost";
            se.m_staminaRegenMultiplier = multiplier;
            se.m_ttl = durationSeconds;

            player.GetSEMan().AddStatusEffect(se, true, 0, 0f);
            Log.LogInfo($"Stamina Regen Boost aplicado vía SEMan: multiplier={multiplier}, ttl={durationSeconds}s.");

            return EffectApplicationResult.Ok();
        }

        private static EffectApplicationResult ApplyComfortBoost(MechanicDto mechanic)
        {
            var bonus = (int)ReadFloat(mechanic, "comfort_bonus", fallback: 2f);

            ComfortBoostState.Bonus = bonus;
            ComfortBoostState.IsActive = true;

            return EffectApplicationResult.Ok();
        }

        private static float ReadFloat(MechanicDto mechanic, string key, float fallback)
        {
            var value = mechanic.Options?[key]?.Value<float?>();
            return value ?? fallback;
        }
    }
}
