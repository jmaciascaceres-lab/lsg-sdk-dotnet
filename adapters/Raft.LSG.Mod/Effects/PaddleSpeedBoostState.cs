using System;

namespace RaftLsgMod.Effects
{
    /// <summary>
    /// Estado del buff activo de Paddle Speed Boost (mmv=66). Los patches de Harmony
    /// se invocan como métodos estáticos, así que el estado no puede vivir dentro de
    /// RaftEffectInterpreter (instancia) sin pasar por indirección extra. Un proceso
    /// de BepInEx = un jugador local, así que el estado estático es seguro aquí
    /// (no es un servidor multi-tenant sirviendo a varios jugadores a la vez).
    /// </summary>
    internal static class PaddleSpeedBoostState
    {
        public static bool IsActive;
        public static float Multiplier = 1f;
        public static DateTimeOffset ExpiresAt;

        public static bool IsCurrentlyActive() => IsActive && DateTimeOffset.UtcNow < ExpiresAt;

        public static void Reset()
        {
            IsActive = false;
            Multiplier = 1f;
        }
    }
}
