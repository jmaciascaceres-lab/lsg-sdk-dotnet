using System.Text.Json;
using CoreKeeper.LSG.Mod.Effects.Patches;
using LSG.SDK.Core.Mechanics;
using LSG.SDK.Core.Models;

namespace CoreKeeper.LSG.Mod.Effects
{
    /// <summary>
    /// Traduce las dos mecánicas mínimas de Core Keeper (mmv=58 Mining Speed Boost,
    /// mmv=59 Reveal Nearby Map) a acciones concretas en el juego.
    ///
    /// - Mining Speed Boost: efecto CON duración -> implementa ITimedEffectInterpreter,
    ///   Apply() activa el multiplicador vía Harmony patch (MiningSpeedPatch/MiningSpeedState),
    ///   Revert() lo desactiva. El SDK-core (TimedEffectTracker) decide CUÁNDO llamar a
    ///   Revert(); este interpreter solo sabe QUÉ hacer.
    ///
    /// - Reveal Nearby Map: efecto instantáneo, sin estado -> Apply() ejecuta la acción
    ///   una vez y retorna; nunca se registra en el tracker (ver Plugin.cs: solo se
    ///   trackea si duration_seconds > 0).
    /// </summary>
    public sealed class CoreKeeperEffectInterpreter : ITimedEffectInterpreter
    {
        // Nombres tal como quedaron en el catálogo tras mechanics/bulk (ver README de LSG.SDK.Core).
        // Usar Name en vez de hardcodear mmv_id acopla menos este código a IDs de una BD
        // específica (dev/staging/prod pueden tener IDs distintos para el mismo juego).
        private const string MiningSpeedBoostName = "Mining Speed Boost";
        private const string RevealNearbyMapName = "Reveal Nearby Map";

        private const float DefaultMiningMultiplier = 1.25f;
        private const int DefaultRevealRadiusBonus = 15;

        public bool CanApply(MechanicDto mechanic) =>
            mechanic.Name is MiningSpeedBoostName or RevealNearbyMapName;

        public EffectApplicationResult Apply(MechanicDto mechanic)
        {
            return mechanic.Name switch
            {
                MiningSpeedBoostName => ApplyMiningSpeedBoost(mechanic),
                RevealNearbyMapName => ApplyRevealNearbyMap(mechanic),
                _ => EffectApplicationResult.Failed($"Mecánica no reconocida por este interpreter: '{mechanic.Name}'"),
            };
        }

        public void Revert(TimedEffect effect)
        {
            switch (effect.Mechanic.Name)
            {
                case MiningSpeedBoostName:
                    MiningSpeedState.Reset();
                    break;

                default:
                    // Reveal Nearby Map nunca debería llegar aquí (es instantáneo, no se
                    // trackea). Si llega, es un bug de wiring en Plugin.cs.
                    break;
            }
        }

        // ---------- Mining Speed Boost (buff, con duración) ----------

        private static EffectApplicationResult ApplyMiningSpeedBoost(MechanicDto mechanic)
        {
            var multiplier = ReadFloatOption(mechanic, "multiplier", DefaultMiningMultiplier);

            if (multiplier == DefaultMiningMultiplier && !HasRealOption(mechanic, "multiplier"))
            {
                // options venía vacío/placeholder (caso ya observado en Subnautica) — se
                // aplica el default pero se avisa, en vez de fallar silenciosamente.
                MiningSpeedState.ActiveMultiplier = DefaultMiningMultiplier;
                return EffectApplicationResult.OkWithWarning(
                    $"'{mechanic.Name}' sin 'multiplier' en options — se aplicó default {DefaultMiningMultiplier}. Revisar catálogo (mmv={mechanic.MmvId}).");
            }

            MiningSpeedState.ActiveMultiplier = multiplier;

            // RevertState no es estrictamente necesario aquí porque Revert() usa un
            // Reset() fijo, pero se deja preparado por si en el futuro se soportan
            // buffs apilables/anidados que necesiten volver al multiplicador anterior
            // (no al default) al expirar el más reciente.
            return EffectApplicationResult.Ok();
        }

        // ---------- Reveal Nearby Map (modifier, instantáneo) ----------

        private static EffectApplicationResult ApplyRevealNearbyMap(MechanicDto mechanic)
        {
            var radiusBonus = ReadIntOption(mechanic, "radius_bonus", DefaultRevealRadiusBonus);

            // *** TODO: verificar contra decompilado. Placeholder de la llamada real
            // al sistema de revelado de mapa de Core Keeper (probablemente un ECS
            // system o un singleton accesible vía MapRevealServicePlaceholder.Instance). ***
            var applied = MapRevealServicePlaceholder.TryRevealAroundPlayer(radiusBonus);

            return applied
                ? EffectApplicationResult.Ok()
                : EffectApplicationResult.Failed("MapRevealServicePlaceholder no disponible — verificar wiring con el juego real.");
        }

        // ---------- Helpers de lectura defensiva de options ----------

        private static bool HasRealOption(MechanicDto mechanic, string propertyName) =>
            mechanic.Options is not null &&
            mechanic.Options.Value.ValueKind == JsonValueKind.Object &&
            mechanic.Options.Value.TryGetProperty(propertyName, out _);

        private static float ReadFloatOption(MechanicDto mechanic, string propertyName, float fallback)
        {
            if (HasRealOption(mechanic, propertyName) &&
                mechanic.Options!.Value.GetProperty(propertyName).TryGetSingle(out var value))
            {
                return value;
            }
            return fallback;
        }

        private static int ReadIntOption(MechanicDto mechanic, string propertyName, int fallback)
        {
            if (HasRealOption(mechanic, propertyName) &&
                mechanic.Options!.Value.GetProperty(propertyName).TryGetInt32(out var value))
            {
                return value;
            }
            return fallback;
        }
    }

    /// <summary>
    /// Placeholder de compilación para el sistema de revelado de mapa.
    /// *** TODO: reemplazar por la API real de Core Keeper una vez verificada en dnSpy. ***
    /// </summary>
    internal static class MapRevealServicePlaceholder
    {
        public static bool TryRevealAroundPlayer(int radiusBonus) => true;
    }
}
