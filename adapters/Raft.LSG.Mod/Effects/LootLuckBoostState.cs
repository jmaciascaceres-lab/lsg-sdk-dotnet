namespace RaftLsgMod.Effects
{
    /// <summary>
    /// Estado del buff "Loot Luck Boost" (mmv=67). Redefinido el 2026-07-10 tras
    /// revisar SO_MysteryPackageLoot en dnSpy: GetRandomItemFromPossibles() elige
    /// UNIFORMEMENTE al azar entre ítems no aprendidos — no existe una noción de
    /// "rareza" o probabilidad ponderada que amplificar. El efecto real es una
    /// GARANTÍA: si el jugador ya aprendió todo (el método devolvería null), el
    /// buff fuerza un fallback devolviendo un ítem igual desde el pool completo,
    /// para que abrir un Mystery Package nunca "se pierda".
    /// </summary>
    internal static class LootLuckBoostState
    {
        public static bool IsActive;

        public static void Reset() => IsActive = false;
    }
}
