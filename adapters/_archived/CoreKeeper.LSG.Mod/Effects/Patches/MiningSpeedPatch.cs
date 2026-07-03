using HarmonyLib;

namespace CoreKeeper.LSG.Mod.Effects.Patches
{
    /// <summary>
    /// Patch para "Mining Speed Boost" (mmv=58).
    ///
    /// *** ADVERTENCIA: nombres ilustrativos, no verificados contra el ensamblado
    /// decompilado de Core Keeper. Antes de compilar contra el juego real:
    ///   1. Abrir Assembly-CSharp.dll con dnSpy/ILSpy.
    ///   2. Ubicar el componente/sistema real que calcula velocidad de picoteo/minería
    ///      (en versiones de Core Keeper con Unity ECS/DOTS, buscar algo como
    ///      "PlayerController", "ToolHandling" o un ECS system con "Mining" en el nombre).
    ///   3. Reemplazar el target del [HarmonyPatch] por la clase/método reales.
    /// Este patch usa Postfix (multiplica el resultado) para no depender de la
    /// firma interna del método original — más resistente a cambios de versión
    /// del juego que un Prefix que reemplace lógica completa.
    /// ***
    /// </summary>
    [HarmonyPatch(typeof(MiningComponentPlaceholder), nameof(MiningComponentPlaceholder.GetMiningSpeed))]
    public static class MiningSpeedPatch
    {
        // ReSharper disable once InconsistentNaming
        public static void Postfix(ref float __result)
        {
            __result *= MiningSpeedState.ActiveMultiplier;
        }
    }

    /// <summary>
    /// Placeholder de compilación — reemplazar por el tipo real del juego una vez
    /// verificado en dnSpy. Se deja aquí solo para que el proyecto compile como
    /// scaffold; NO es parte del juego real.
    /// </summary>
    public class MiningComponentPlaceholder
    {
        public float GetMiningSpeed() => 1.0f;
    }
}
