using LSG.SDK.Core.Models;

namespace LSG.SDK.Core.Mechanics
{
    /// <summary>
    /// Contrato que cada adaptador de juego (Core Keeper, Valheim, Subnautica, VRising...)
    /// implementa para traducir una MechanicDto (name/type/options genéricos) a la mecánica
    /// concreta del juego (Harmony patch, evento nativo, etc.).
    ///
    /// El SDK-core NUNCA conoce la API del juego; solo entrega el contrato validado.
    /// Cada adaptador vive en su propio repo/mod-project y referencia LSG.SDK.Core como
    /// dependencia — así se mantiene el mantenimiento independiente por juego.
    /// </summary>
    public interface IEffectInterpreter
    {
        /// <summary>
        /// true si este intérprete sabe aplicar la mecánica dada (por nombre o tipo).
        /// Permite que un adaptador soporte solo un subconjunto del catálogo.
        /// </summary>
        bool CanApply(MechanicDto mechanic);

        /// <summary>
        /// Aplica el efecto en el juego. Se invoca SOLO después de un RedeemResponse
        /// exitoso — el SDK-core ya debitó los puntos; esta llamada nunca debe fallar
        /// de forma que deje al jugador sin puntos y sin efecto (ver EffectApplicationResult).
        /// </summary>
        EffectApplicationResult Apply(MechanicDto mechanic);
    }

    public sealed class EffectApplicationResult
    {
        public bool Success { get; init; }
        public string? Warning { get; init; }

        public static EffectApplicationResult Ok() => new() { Success = true };

        public static EffectApplicationResult OkWithWarning(string warning) =>
            new() { Success = true, Warning = warning };

        public static EffectApplicationResult Failed(string reason) =>
            new() { Success = false, Warning = reason };
    }
}
