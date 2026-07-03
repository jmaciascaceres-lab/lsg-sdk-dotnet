using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LSG.SDK.Core.Models
{
    public sealed class LoginResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;

        [JsonPropertyName("expires_in_seconds")]
        public int ExpiresInSeconds { get; set; }

        [JsonPropertyName("expires_at")]
        public DateTimeOffset ExpiresAt { get; set; }

        [JsonPropertyName("player")]
        public PlayerSummary Player { get; set; } = new();
    }

    public sealed class PlayerSummary
    {
        [JsonPropertyName("id_players")]
        public int IdPlayers { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("age")]
        public int Age { get; set; }

        [JsonPropertyName("roles")]
        public List<string> Roles { get; set; } = new();
    }

    public sealed class TokenRemainingResponse
    {
        [JsonPropertyName("expires_in_seconds")]
        public int ExpiresInSeconds { get; set; }
    }
}
