using System;
using ICities;

namespace CitiesSkylinesLsgMod
{
    /// <summary>
    /// ThreadingExtensionBase (implementa IThreadingExtension) confirmado
    /// como el hook estándar para trabajo periódico en Cities: Skylines -
    /// documentación oficial + tutoriales de la comunidad. El juego detecta
    /// esta clase automáticamente por reflexión (igual que Mod : IUserMod),
    /// sin necesitar registro manual.
    ///
    /// OnUpdate se llama cada frame, no cada segundo - se throttlea acá
    /// mismo para no llamar Tick()/flush con más frecuencia de la necesaria.
    /// </summary>
    public sealed class ThreadingExtension : ThreadingExtensionBase
    {
        private float _accumulatedSeconds;
        private const float TickIntervalSeconds = 1f;

        public override void OnUpdate(float realTimeDelta, float simulationTimeDelta)
        {
            if (Mod.TimedEffects is null)
                return; // Mod.OnEnabled() todavía no corrió

            _accumulatedSeconds += realTimeDelta;
            if (_accumulatedSeconds < TickIntervalSeconds)
                return;
            _accumulatedSeconds = 0f;

            try
            {
                Mod.TimedEffects.Tick();

                if (Mod.PlayerId.HasValue && Mod.LsgConfig is not null &&
                    (DateTimeOffset.UtcNow - Mod.LastOfflineFlush).TotalSeconds >= Mod.LsgConfig.OfflineFlushIntervalSeconds)
                {
                    Mod.LastOfflineFlush = DateTimeOffset.UtcNow;
                    _ = FlushOfflineQueueAsync(Mod.PlayerId.Value);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LSG] Fallo en ThreadingExtension.OnUpdate: {ex}");
            }
        }

        private async System.Threading.Tasks.Task FlushOfflineQueueAsync(int playerId)
        {
            try
            {
                var pending = await Mod.OfflineQueue!.FlushAsync(playerId);
                if (pending > 0)
                    UnityEngine.Debug.LogWarning($"[LSG] Cola offline: {pending} evento(s) siguen pendientes.");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LSG] Fallo al sincronizar cola offline: {ex}");
            }
        }
    }
}
