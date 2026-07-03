using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using LSG.SDK.Core.Api;
using LSG.SDK.Core.Config;
using LSG.SDK.Core.Models;

namespace LSG.SDK.Core.Offline
{
    /// <summary>
    /// Buffer en memoria de eventos generados sin conexión. El flush real lo resuelve
    /// POST /offline/sync en el server (idempotente por client_ref, ventana 30 días) —
    /// este componente solo se encarga de no perder eventos si la llamada falla y de
    /// reintentar periódicamente. Rol "player" confirmado como suficiente (ROLE_ALL).
    /// </summary>
    public sealed class OfflineQueue
    {
        private readonly LsgCoreApiClient _api;
        private readonly LsgConfig _config;
        private readonly ConcurrentQueue<OfflineEventDto> _pending = new();

        public OfflineQueue(LsgCoreApiClient api, LsgConfig config)
        {
            _api = api;
            _config = config;
        }

        /// <summary>Encola un evento sin bloquear (llamar desde el hook del juego, ej. al
        /// completar una quest offline). No lanza excepciones.</summary>
        public void Enqueue(OfflineEventDto evt) => _pending.Enqueue(evt);

        /// <summary>
        /// Intenta sincronizar todo lo pendiente. Seguro de llamar repetidamente
        /// (idempotente vía client_ref). Devuelve la cantidad de eventos que quedaron
        /// pendientes tras el intento (0 = todo sincronizado o descartado por el server).
        /// </summary>
        public async Task<int> FlushAsync(int playerId, CancellationToken ct = default)
        {
            if (_pending.IsEmpty)
                return 0;

            var batch = new List<OfflineEventDto>();
            while (_pending.TryDequeue(out var evt))
                batch.Add(evt);

            var request = new OfflineSyncRequestDto
            {
                PlayerId = playerId,
                GameId = _config.GameId,
                Events = batch,
            };

            try
            {
                await _api.SyncOfflineAsync(request, ct);
                // El server procesa cada evento independientemente (SYNCED/DUPLICATE/REJECTED).
                // Para v1 asumimos éxito de la llamada = batch procesado; si se requiere
                // reintentar solo los REJECTED, parsear la respuesta 207 e re-encolar
                // selectivamente en una iteración futura del SDK.
                return 0;
            }
            catch (LsgApiException)
            {
                // Falla de red/servidor: re-encolar todo el batch para el próximo intento.
                foreach (var evt in batch)
                    _pending.Enqueue(evt);
                return _pending.Count;
            }
        }

        public int PendingCount => _pending.Count;
    }
}
