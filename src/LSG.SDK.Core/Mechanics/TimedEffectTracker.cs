using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using LSG.SDK.Core.Models;

namespace LSG.SDK.Core.Mechanics
{
    public sealed class TimedEffect
    {
        public int PlayerId { get; set; }
        public MechanicDto Mechanic { get; set; } = null!;
        public DateTimeOffset ExpiresAt { get; set; }

        /// <summary>Estado necesario para revertir el efecto (ej. valor original antes
        /// del buff). Lo llena el adaptador en IEffectInterpreter.Apply() y lo recibe
        /// de vuelta en Revert() al expirar — el SDK-core solo lo transporta.</summary>
        public object? RevertState { get; set; }

        public string Key => $"{PlayerId}:{Mechanic.MmvId}";
    }

    /// <summary>
    /// Contraparte de IEffectInterpreter.Apply() para efectos con duración:
    /// el adaptador que registra un TimedEffect debe saber revertirlo.
    /// </summary>
    public interface ITimedEffectInterpreter : IEffectInterpreter
    {
        void Revert(TimedEffect effect);
    }

    public interface ITimedEffectTracker
    {
        void Track(TimedEffect effect);
        bool IsActive(int playerId, int mmvId);
        IReadOnlyCollection<TimedEffect> GetActive();

        /// <summary>Se dispara cuando un efecto vence. El suscriptor (normalmente el
        /// propio Plugin/adaptador) debe llamar a ITimedEffectInterpreter.Revert(effect).</summary>
        event Action<TimedEffect>? OnExpired;

        /// <summary>Debe llamarse periódicamente desde el loop del juego (ej. Update()
        /// de Unity, o un timer del mod-loader). El SDK-core no asume ningún game loop.</summary>
        void Tick();
    }

    /// <summary>
    /// Implementación default en memoria. Un solo Tick() barre todos los efectos
    /// activos — suficiente para el volumen esperado (decenas de efectos por jugador,
    /// no miles). Sin persistencia: si el proceso del mod se reinicia, los efectos
    /// activos se pierden (limitación conocida, documentada en el README del SDK).
    /// </summary>
    public sealed class TimedEffectTracker : ITimedEffectTracker
    {
        private readonly IGameClock _clock;
        private readonly ConcurrentDictionary<string, TimedEffect> _active = new();

        public event Action<TimedEffect>? OnExpired;

        public TimedEffectTracker(IGameClock? clock = null)
        {
            _clock = clock ?? new SystemClock();
        }

        public void Track(TimedEffect effect) => _active[effect.Key] = effect;

        public bool IsActive(int playerId, int mmvId) =>
            _active.TryGetValue($"{playerId}:{mmvId}", out var e) && e.ExpiresAt > _clock.UtcNow;

        public IReadOnlyCollection<TimedEffect> GetActive() => _active.Values.ToList();

        public void Tick()
        {
            var now = _clock.UtcNow;
            foreach (var kvp in _active)
            {
                if (kvp.Value.ExpiresAt > now)
                    continue;

                if (_active.TryRemove(kvp.Key, out var expired))
                    OnExpired?.Invoke(expired);
            }
        }
    }
}
