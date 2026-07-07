using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using LSG.SDK.Core.Config;
using LSG.SDK.Core.Models;

namespace LSG.SDK.Core.Auth
{
    /// <summary>
    /// Maneja login, refresh proactivo y exposición del token vigente.
    /// Thread-safe para el caso simple de un único jugador por proceso de mod.
    /// </summary>
    public sealed class LsgAuthClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly LsgConfig _config;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        private LoginResponse? _session;
        private DateTimeOffset _expiresAt;

        public LsgAuthClient(LsgConfig config, HttpClient? httpClient = null)
        {
            _config = config;
            _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(config.HttpTimeoutSeconds) };
        }

        public PlayerSummary? CurrentPlayer => _session?.Player;

        /// <summary>
        /// Login inicial con credenciales del jugador. El mod NUNCA debe persistir la
        /// contraseña en texto plano; solicitarla una vez y descartarla tras este llamado.
        /// </summary>
        public async Task<LoginResponse> LoginAsync(string email, string password, CancellationToken ct = default)
        {
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = email,
                ["password"] = password,
            };

            using var content = new FormUrlEncodedContent(form);
            using var response = await _http.PostAsync($"{_config.AuthBaseUrl}/login", content, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var login = JsonConvert.DeserializeObject<LoginResponse>(json)
                        ?? throw new InvalidOperationException("Respuesta de login vacía.");

            _session = login;
            _expiresAt = login.ExpiresAt;
            return login;
        }

        /// <summary>
        /// Devuelve un token válido, refrescándolo proactivamente si está por expirar.
        /// Llamar antes de cada request a LSG-Core-API en vez de cachear el token manualmente.
        /// </summary>
        public async Task<string> GetValidTokenAsync(CancellationToken ct = default)
        {
            if (_session is null)
                throw new InvalidOperationException("No hay sesión activa. Llama a LoginAsync primero.");

            var secondsLeft = (_expiresAt - DateTimeOffset.UtcNow).TotalSeconds;
            if (secondsLeft > _config.TokenRefreshThresholdSeconds)
                return _session.AccessToken;

            await _refreshLock.WaitAsync(ct);
            try
            {
                // Doble chequeo por si otro hilo ya refrescó mientras esperábamos el lock.
                secondsLeft = (_expiresAt - DateTimeOffset.UtcNow).TotalSeconds;
                if (secondsLeft > _config.TokenRefreshThresholdSeconds)
                    return _session.AccessToken;

                await RefreshAsync(ct);
                return _session!.AccessToken;
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        private async Task RefreshAsync(CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_config.AuthBaseUrl}/token/refresh");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session!.AccessToken);

            using var response = await _http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                // Si el refresh falla (token ya expirado más allá de lo tolerado por el server),
                // el mod debe re-solicitar login interactivo al jugador. No reintentar en loop.
                throw new InvalidOperationException(
                    $"No se pudo refrescar el token (HTTP {(int)response.StatusCode}). Se requiere nuevo login.");
            }

            var json = await response.Content.ReadAsStringAsync();
            var refreshed = JsonConvert.DeserializeObject<LoginResponse>(json)
                             ?? throw new InvalidOperationException("Respuesta de refresh vacía.");

            _session = refreshed;
            _expiresAt = refreshed.ExpiresAt;
        }


        public void Dispose() => _refreshLock.Dispose();
    }
}
