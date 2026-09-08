using ICities;

namespace CitiesSkylinesLsgMod
{
    /// <summary>
    /// Confirmado en la documentación oficial de la API (EconomyExtensionBase,
    /// namespace ICities): "OnUpdateMoneyAmount" es el hook que el juego
    /// invoca para leer/sincronizar el dinero interno de la ciudad. Mismo
    /// mecanismo que usa el mod real "UnlimitedMoney" (confirmado en un
    /// decompile público de ese mod).
    ///
    /// Se detecta automáticamente por reflexión, igual que Mod/ThreadingExtension.
    /// </summary>
    public sealed class EconomyExtension : EconomyExtensionBase
    {
        // Bonus pendiente de aplicar (seteado por CitiesSkylinesEffectInterpreter
        // al canjear). Se aplica UNA sola vez y se limpia - este hook se llama
        // repetidamente por el juego, no solo una vez.
        internal static long PendingBonus;

        public override long OnUpdateMoneyAmount(long internalMoneyAmount)
        {
            if (PendingBonus == 0)
                return internalMoneyAmount;

            var bonus = PendingBonus;
            PendingBonus = 0;

            UnityEngine.Debug.Log($"[LSG] Cash Income Bonus aplicado: {internalMoneyAmount} -> {internalMoneyAmount + bonus} (+{bonus}).");
            return internalMoneyAmount + bonus;
        }
    }
}
