using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

        public Task<JObject?> SyncOfflineAsync(OfflineSyncRequestDto req, CancellationToken ct = default) =>
            PostAsync<JObject>("/offline/sync", req, ct);

        // ---------- M2: conectividad por juego (no invocar desde SDK-core genérico) ----------

        public Task<JObject?> ConnectAsync(int playerId, ConnectRequestDto req, CancellationToken ct = default) =>
            PostAsync<JObject>($"/videogames/{_config.GameId}/players/{playerId}/connect", req, ct);

        public Task<JObject?> StartSessionAsync(int playerId, SessionStartRequestDto req, CancellationToken ct = default) =>
            PostAsync<JObject>($"/videogames/{_config.GameId}/players/{playerId}/sessions", req, ct);

        public Task<JObject?> EndSessionAsync(int playerId, int sessionId, SessionEndRequestDto req, CancellationToken ct = default) =>
            PostAsync<JObject>($"/videogames/{_config.GameId}/players/{playerId}/sessions/{sessionId}/end", req, ct, useHttpPatch: true);

        // ---------- Helpers HTTP ----------
        // NOTA (2026-07-05): se migró de System.Text.Json a Newtonsoft.Json —
        // System.Text.Json (DeserializeAsync, JsonTypeInfo<T>, Utf8JsonWriter)
        // produce VTable/InvalidProgramException en el Mono viejo de BepInEx
        // (CLR 4.0.30319). Newtonsoft.Json es el estándar probado para este
        // entorno. JObject reemplaza a JsonElement (era struct; JObject es clase,
        // ya no hace falta un PostRawAsync separado para evitar "where T : class").

        private async Task<T?> GetAsync<T>(string path, CancellationToken ct) where T : class
        {
            var token = await _auth.GetValidTokenAsync(ct);
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_config.CoreApiBaseUrl}{path}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(json);
        }

        private async Task<T?> PostAsync<T>(string path, object body, CancellationToken ct, bool useHttpPatch = false) where T : class
        {
            var token = await _auth.GetValidTokenAsync(ct);
            var method = useHttpPatch ? HttpMethod.Patch : HttpMethod.Post;
            var json = JsonConvert.SerializeObject(body);

            using var request = new HttpRequestMessage(method, $"{_config.CoreApiBaseUrl}{path}")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new LsgApiException((int)response.StatusCode, responseBody);

            return JsonConvert.DeserializeObject<T>(responseBody);
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
