namespace LSG.SDK.Core.Mechanics
{
    /// <summary>
    /// Abstracción del "reloj" contra el que expiran los efectos temporales.
    /// Default: reloj real (UtcNow). Un adaptador puede implementar su propio
    /// IGameClock si el juego pausa el tiempo (ej. menú de pausa) y no quiere que
    /// eso consuma duración del buff, o si prefiere anclar la expiración a ticks
    /// de juego en vez de tiempo real.
    /// </summary>
    public interface IGameClock
    {
        DateTimeOffset UtcNow { get; }
    }

    /// <summary>Implementación default: reloj real del sistema. Suficiente para
    /// el set mínimo actual (Core Keeper, Valheim, Subnautica, VRising usan
    /// duration_seconds en tiempo real, no en ticks de juego).</summary>
    public sealed class SystemClock : IGameClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
