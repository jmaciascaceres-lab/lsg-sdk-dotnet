using BepInEx.Logging;
using HarmonyLib;

namespace RaftLsgMod.Effects.Patches
{
    /// <summary>
    /// Intercepta SO_MysteryPackageLoot.GetRandomItemFromPossibles() para
    /// garantizar un ítem cuando el buff "Loot Luck Boost" (mmv=67) está activo
    /// y el método original hubiera devuelto null (jugador ya aprendió todos los
    /// ítems no ocultos disponibles).
    ///
    /// POR QUÉ AQUÍ: es el método público real que consume el juego para resolver
    /// qué ítem entrega un Mystery Package — visto completo en dnSpy 2026-07-10.
    /// GetRandomItemFromPossibles() ya filtra por "no aprendido" y elige uniforme
    /// al azar; no hay rareza que amplificar (ver LootLuckBoostState.cs), así que
    /// el Postfix solo actúa quir el resultado original es null.
    ///
    /// `possibleYield` es un campo público (List&lt;Item_Base&gt;) — no requiere
    /// reflexión, a diferencia de `playerNetwork` en PaddleForcePatch.
    /// </summary>
    [HarmonyPatch(typeof(SO_MysteryPackageLoot), nameof(SO_MysteryPackageLoot.GetRandomItemFromPossibles))]
    internal static class LootLuckPatch
    {
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("RaftLsgMod.LootLuckPatch");

        private static void Postfix(SO_MysteryPackageLoot __instance, ref Item_Base __result)
        {
            if (!LootLuckBoostState.IsActive)
                return;

            if (__result is not null)
                return; // Ya había un ítem válido — nada que garantizar.

            var pool = __instance.possibleYield;
            if (pool is null || pool.Count == 0)
                return; // Ni siquiera hay pool completo — no hay nada que devolver.

            var index = UnityEngine.Random.Range(0, pool.Count);
            __result = pool[index];
            Log.LogInfo($"Loot Luck Boost aplicado: se garantizó '{__result.UniqueName}' (habría sido null sin el buff).");
        }
    }
}
