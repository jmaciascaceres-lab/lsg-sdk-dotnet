namespace ValheimLsgMod.Effects
{
    /// <summary>
    /// Estado del buff "Comfort Boost" (mmv=61). A diferencia de Stamina Regen
    /// Boost (que usa el sistema nativo SEMan.ModifyStaminaRegen), el comfort de
    /// Valheim se RECALCULA DESDE CERO cada ~2s en base a piezas cercanas
    /// (SE_Rested.CalculateComfortLevel) — no es un valor acumulable, así que
    /// necesita un Harmony patch que sume un bonus mientras esté activo.
    /// </summary>
    internal static class ComfortBoostState
    {
        public static bool IsActive;
        public static int Bonus = 2;

        public static void Reset()
        {
            IsActive = false;
            Bonus = 2;
        }
    }
}
