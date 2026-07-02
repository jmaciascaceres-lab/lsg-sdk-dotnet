using LSG.SDK.Core.Api;
using LSG.SDK.Core.Models;

namespace LSG.SDK.Core.Mechanics
{
    /// <summary>
    /// Cachea el catálogo de mecánicas del juego al iniciar el mod (evita golpear la API
    /// en cada canje). Refrescar manualmente con RefreshAsync si el catálogo cambió
    /// (ej. tras un nuevo mechanics/bulk en el core).
    /// </summary>
    public sealed class MechanicsCache
    {
        private readonly LsgCoreApiClient _api;
        private Dictionary<int, MechanicDto> _byMmvId = new();

        /// <summary>Se dispara cuando se detecta una mecánica con options vacío/placeholder.
        /// Útil para loguear o para reportar telemetría de calidad de catálogo.</summary>
        public event Action<MechanicDto>? OnPlaceholderOptionsDetected;

        public MechanicsCache(LsgCoreApiClient api)
        {
            _api = api;
        }

        public async Task RefreshAsync(CancellationToken ct = default)
        {
            var mechanics = await _api.GetVideogameMechanicsAsync(ct) ?? new List<MechanicDto>();
            var map = new Dictionary<int, MechanicDto>();

            foreach (var m in mechanics)
            {
                if (m.HasPlaceholderOrEmptyOptions())
                    OnPlaceholderOptionsDetected?.Invoke(m);

                map[m.MmvId] = m;
            }

            _byMmvId = map;
        }

        public MechanicDto? Get(int modifiableMechanicVideogameId) =>
            _byMmvId.TryGetValue(modifiableMechanicVideogameId, out var m) ? m : null;

        public IReadOnlyCollection<MechanicDto> All => _byMmvId.Values;

        /// <summary>Filtra por tipo (buff, nerf, speed, health, economy, modifier).</summary>
        public IEnumerable<MechanicDto> ByType(string type) =>
            _byMmvId.Values.Where(m => string.Equals(m.Type, type, StringComparison.OrdinalIgnoreCase));
    }
}
