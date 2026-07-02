using System.Text.Json.Serialization;

namespace LSG.SDK.Core.Models
{
    public sealed class OfflineEventDto
    {
        /// <summary>Identificador único generado por el cliente para idempotencia server-side.</summary>
        [JsonPropertyName("client_ref")]
        public string ClientRef { get; set; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("client_generated_at")]
        public DateTimeOffset ClientGeneratedAt { get; set; } = DateTimeOffset.UtcNow;

        [JsonPropertyName("point_dimension_id")]
        public int PointDimensionId { get; set; }

        [JsonPropertyName("direction")]
        public string Direction { get; set; } = "CREDIT"; // CREDIT | DEBIT

        [JsonPropertyName("amount")]
        public int Amount { get; set; }

        [JsonPropertyName("source_type")]
        public string SourceType { get; set; } = "OFFLINE_GAME";

        [JsonPropertyName("payload")]
        public Dictionary<string, object>? Payload { get; set; }
    }

    public sealed class OfflineSyncRequestDto
    {
        [JsonPropertyName("player_id")]
        public int PlayerId { get; set; }

        [JsonPropertyName("game_id")]
        public int GameId { get; set; }

        [JsonPropertyName("events")]
        public List<OfflineEventDto> Events { get; set; } = new();
    }

    public sealed class ConnectRequestDto
    {
        [JsonPropertyName("lsg_enabled")]
        public bool LsgEnabled { get; set; } = true;

        [JsonPropertyName("plugin_version")]
        public string PluginVersion { get; set; } = string.Empty;

        [JsonPropertyName("settings")]
        public Dictionary<string, object>? Settings { get; set; }
    }

    public sealed class SessionStartRequestDto
    {
        [JsonPropertyName("started_at")]
        public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

        [JsonPropertyName("session_metrics")]
        public Dictionary<string, object>? SessionMetrics { get; set; }

        [JsonPropertyName("plugin_version")]
        public string PluginVersion { get; set; } = string.Empty;

        [JsonPropertyName("settings")]
        public Dictionary<string, object>? Settings { get; set; }
    }

    public sealed class SessionEndRequestDto
    {
        [JsonPropertyName("ended_at")]
        public DateTimeOffset EndedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
