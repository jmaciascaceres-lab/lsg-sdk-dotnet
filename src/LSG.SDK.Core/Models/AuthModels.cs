using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace LSG.SDK.Core.Models
{
    public sealed class LoginResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonProperty("token_type")]
        public string TokenType { get; set; } = string.Empty;

        [JsonProperty("expires_in_seconds")]
        public int ExpiresInSeconds { get; set; }

        [JsonProperty("expires_at")]
        public DateTimeOffset ExpiresAt { get; set; }

        [JsonProperty("player")]
        public PlayerSummary Player { get; set; } = new();
    }

    public sealed class PlayerSummary
    {
        [JsonProperty("id_players")]
        public int IdPlayers { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("email")]
        public string Email { get; set; } = string.Empty;

        [JsonProperty("age")]
        public int? Age { get; set; }

        [JsonProperty("roles")]
        public List<string> Roles { get; set; } = new();
    }

    public sealed class TokenRemainingResponse
    {
        [JsonProperty("expires_in_seconds")]
        public int ExpiresInSeconds { get; set; }
    }
}
