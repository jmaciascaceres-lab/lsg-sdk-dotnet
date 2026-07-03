using System;

namespace RaftLsgMod.Effects
{
    /// <summary>
    /// Estado del buff activo de Paddle Speed Boost (mmv=66). Los patches de Harmony
    /// se invocan como métodos estáticos, así que el estado no puede vivir dentro de
    /// RaftEffectInterpreter (instancia) sin pasar por indirección extra. Un proceso
    /// de BepInEx = un jugador local, así que el estado estático es seguro aquí
    /// (no es un servidor multi-tenant sirviendo a varios jugadores a la vez).
    ///
    /// IMPORTANTE: la expiración NO se controla aquí — la única fuente de verdad es
    /// TimedEffectTracker (SDK-core), que dispara OnExpired -> RaftEffectInterpreter.Revert()
    /// -> PaddleSpeedBoostState.Reset(). Este archivo tuvo antes un ExpiresAt local
    /// nunca asignado (CS0649) que habría dejado IsActive=true indefinidamente sin
    /// afectar nada, porque el chequeo comparaba contra la fecha por defecto — bug
    /// silencioso removido el 2026-07-03, no reintroducir un segundo mecanismo de
    /// expiración aquí.
    /// </summary>
    internal static class PaddleSpeedBoostState
    {
        public static bool IsActive;
        public static float Multiplier = 1f;

        public static void Reset()
        {
            IsActive = false;
            Multiplier = 1f;
        }
    }
}
