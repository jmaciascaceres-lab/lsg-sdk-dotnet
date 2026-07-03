using System;
using System.Collections.Generic;
using LSG.SDK.Core.Models;

namespace LSG.SDK.Core.Mechanics
{
    /// <summary>
    /// Contexto de juego relevante al momento de aplicar un efecto. Cada adaptador
    /// decide qué campos completar (ej. Valheim podría exponer dificultad de mundo;
    /// Core Keeper podría no necesitar ninguno y usar el resolver default).
    /// </summary>
    public sealed class EffectContext
    {
        public int PlayerId { get; set; }

        /// <summary>Identificador libre de dificultad/modo, si el juego lo expone
        /// (ej. "hardcore", "creative", "world_speed_2x"). No estandarizado a
        /// propósito: cada adaptador define su propio vocabulario.</summary>
        public string? DifficultyTag { get; set; }

        /// <summary>Bag libre para contexto adicional específico del adaptador,
        /// sin forzar cambios en EffectContext cada vez que un juego necesita algo distinto.</summary>
        public IReadOnlyDictionary<string, object>? Extra { get; set; }
    }

    /// <summary>
    /// Traduce la duración base declarada en el catálogo (options.duration_seconds)
    /// a la duración efectiva a aplicar. El SDK-core NO tiene una implementación
    /// "inteligente" — solo el pass-through. Cada adaptador que necesite escalar por
    /// dificultad, ciclo día/noche, etc. implementa su propio IDurationResolver.
    /// </summary>
    public interface IDurationResolver
    {
        TimeSpan Resolve(MechanicDto mechanic, EffectContext context);
    }

    /// <summary>
    /// Resolver default: toma options.duration_seconds tal cual, sin escalar.
    /// Si la mecánica no trae duration_seconds (ej. es "modifier" instantáneo,
    /// no "buff"), devuelve TimeSpan.Zero — el llamador decide si eso significa
    /// "no trackear como timed effect".
    /// </summary>
    public sealed class PassthroughDurationResolver : IDurationResolver
    {
        public TimeSpan Resolve(MechanicDto mechanic, EffectContext context)
        {
            if (mechanic.Options is null)
                return TimeSpan.Zero;

            if (mechanic.Options.Value.TryGetProperty("duration_seconds", out var prop) &&
                prop.TryGetInt32(out var seconds))
            {
                return TimeSpan.FromSeconds(seconds);
            }

            return TimeSpan.Zero;
        }
    }
}
