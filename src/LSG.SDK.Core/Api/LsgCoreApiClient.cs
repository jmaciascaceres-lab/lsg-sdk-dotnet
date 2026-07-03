using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LSG.SDK.Core.Auth;
using LSG.SDK.Core.Config;
using LSG.SDK.Core.Models;

namespace LSG.SDK.Core.Api
{
    /// <summary>
    /// Wrapper delgado sobre lsg-core-api. Cada método adjunta el Bearer vigente
    /// obtenido de LsgAuthClient (con refresh automático transparente).
    ///
    /// Alcance M1 (SDK-core, sin conexión a juego todavía):
    ///   - Perfil / saldo de puntos
    ///   - Catálogo de mecánicas
    ///   - Canje (preview + redeem)
    ///   - Cola offline
    /// connect() / sessions() se dejan disponibles pero pertenecen a M2
    /// (conectividad por juego), a invocar solo cuando el adaptador del juego lo requiera.
    /// </summary>
    public sealed class LsgCoreApiClient
    {
        private readonly HttpClient _http;
        private readonly LsgConfig _config;
        private readonly LsgAuthClient _auth;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        public LsgCoreApiClient(LsgConfig config, LsgAuthClient auth, HttpClient? httpClient = null)
        {
            _config = config;
            _auth = auth;
            _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(config.HttpTimeoutSeconds) };
        }

        // ---------- Perfil / saldo ----------

        public Task<PlayerSummary?> GetPlayerAsync(int playerId, CancellationToken ct = default) =>
            GetAsync<PlayerSummary>($"/players/{playerId}", ct);

        public Task<List<PointsBalanceEntry>?> GetPointsBalanceAsync(int playerId, CancellationToken ct = default) =>
            GetAsync<List<PointsBalanceEntry>>($"/players/{playerId}/points/balance", ct);

        // ---------- Catálogo de mecánicas ----------

        public Task<List<MechanicDto>?> GetVideogameMechanicsAsync(CancellationToken ct = default) =>
            GetAsync<List<MechanicDto>>($"/videogames/{_config.GameId}/mechanics", ct);

        // ---------- Canje ----------

        public Task<RedeemPreviewResponse?> PreviewRedeemAsync(int playerId, RedeemRequestDto req, CancellationToken ct = default) =>
            PostAsync<RedeemPreviewResponse>($"/videogames/{_config.GameId}/players/{playerId}/redeem/preview", req, ct);

        public Task<RedeemResponse?> RedeemAsync(int playerId, RedeemRequestDto req, CancellationToken ct = default) =>
            PostAsync<RedeemResponse>($"/videogames/{_config.GameId}/players/{playerId}/redeem", req, ct);

        // ---------- Offline ----------

        public Task<JsonElement?> SyncOfflineAsync(OfflineSyncRequestDto req, CancellationToken ct = default) =>
            PostAsync<JsonElement>("/offline/sync", req, ct);

        // ---------- M2: conectividad por juego (no invocar desde SDK-core genérico) ----------

        public Task<JsonElement?> ConnectAsync(int playerId, ConnectRequestDto req, CancellationToken ct = default) =>
            PostAsync<JsonElement>($"/videogames/{_config.GameId}/players/{playerId}/connect", req, ct);

        public Task<JsonElement?> StartSessionAsync(int playerId, SessionStartRequestDto req, CancellationToken ct = default) =>
            PostAsync<JsonElement>($"/videogames/{_config.GameId}/players/{playerId}/sessions", req, ct);

        public Task<JsonElement?> EndSessionAsync(int playerId, int sessionId, SessionEndRequestDto req, CancellationToken ct = default) =>
            PostAsync<JsonElement>($"/videogames/{_config.GameId}/players/{playerId}/sessions/{sessionId}/end", req, ct, useHttpPatch: true);

        // ---------- Helpers HTTP ----------

        private async Task<T?> GetAsync<T>(string path, CancellationToken ct) where T : class
        {
            var token = await _auth.GetValidTokenAsync(ct);
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_config.CoreApiBaseUrl}{path}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(JsonOpts, ct);
        }

        private async Task<T?> PostAsync<T>(string path, object body, CancellationToken ct, bool useHttpPatch = false) where T : class
        {
            var token = await _auth.GetValidTokenAsync(ct);
            var method = useHttpPatch ? HttpMethod.Patch : HttpMethod.Post;
            using var request = new HttpRequestMessage(method, $"{_config.CoreApiBaseUrl}{path}")
            {
                Content = JsonContent.Create(body, options: JsonOpts),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                throw new LsgApiException((int)response.StatusCode, errorBody);
            }

            return await response.Content.ReadFromJsonAsync<T>(JsonOpts, ct);
        }
    }

    public sealed class LsgApiException : Exception
    {
        public int StatusCode { get; }
        public string ResponseBody { get; }

        public LsgApiException(int statusCode, string responseBody)
            : base($"LSG API error {statusCode}: {responseBody}")
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }
    }
}
