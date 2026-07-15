using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ValheimLsgMod.Effects.Patches
{
    /// <summary>
    /// Intercepta SE_Rested.CalculateComfortLevel(bool, Vector3) — el overload
    /// estático que realmente calcula el comfort en base a piezas cercanas
    /// (fogatas, sillas, etc.). El overload CalculateComfortLevel(Player) delega
    /// a este, así que un solo patch cubre ambos caminos de llamada.
    ///
    /// Confirmado en dnSpy (2026-07-10): el comfort NO es un valor acumulable —
    /// se recalcula desde cero cada ~2s (Player.UpdateBaseValue). Por eso, a
    /// diferencia de Stamina Regen Boost, esta mecánica sí requiere Harmony:
    /// no hay un campo persistente donde sumar un multiplicador.
    /// </summary>
    [HarmonyPatch(typeof(SE_Rested), nameof(SE_Rested.CalculateComfortLevel), typeof(bool), typeof(Vector3))]
    internal static class ComfortBoostPatch
    {
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("ValheimLsgMod.ComfortBoostPatch");

        private static void Postfix(ref int __result)
        {
            if (!ComfortBoostState.IsActive)
                return;

            var original = __result;
            __result += ComfortBoostState.Bonus;
            Log.LogInfo($"Comfort Boost aplicado: comfort {original} -> {__result} (+{ComfortBoostState.Bonus}).");
        }
    }
}
