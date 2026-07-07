using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace LSG.SDK.Core.Models
{
    public sealed class OfflineEventDto
    {
        /// <summary>Identificador único generado por el cliente para idempotencia server-side.</summary>
        [JsonProperty("client_ref")]
        public string ClientRef { get; set; } = Guid.NewGuid().ToString("N");

        [JsonProperty("client_generated_at")]
        public DateTimeOffset ClientGeneratedAt { get; set; } = DateTimeOffset.UtcNow;

        [JsonProperty("point_dimension_id")]
        public int PointDimensionId { get; set; }

        [JsonProperty("direction")]
        public string Direction { get; set; } = "CREDIT"; // CREDIT | DEBIT

        [JsonProperty("amount")]
        public int Amount { get; set; }

        [JsonProperty("source_type")]
        public string SourceType { get; set; } = "OFFLINE_GAME";

        [JsonProperty("payload")]
        public Dictionary<string, object>? Payload { get; set; }
    }

    public sealed class OfflineSyncRequestDto
    {
        [JsonProperty("player_id")]
        public int PlayerId { get; set; }

        [JsonProperty("game_id")]
        public int GameId { get; set; }

        [JsonProperty("events")]
        public List<OfflineEventDto> Events { get; set; } = new();
    }

    public sealed class ConnectRequestDto
    {
        [JsonProperty("lsg_enabled")]
        public bool LsgEnabled { get; set; } = true;

        [JsonProperty("plugin_version")]
        public string PluginVersion { get; set; } = string.Empty;

        [JsonProperty("settings")]
        public Dictionary<string, object>? Settings { get; set; }
    }

    public sealed class SessionStartRequestDto
    {
        [JsonProperty("started_at")]
        public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

        [JsonProperty("session_metrics")]
        public Dictionary<string, object>? SessionMetrics { get; set; }

        [JsonProperty("plugin_version")]
        public string PluginVersion { get; set; } = string.Empty;

        [JsonProperty("settings")]
        public Dictionary<string, object>? Settings { get; set; }
    }

    public sealed class SessionEndRequestDto
    {
        [JsonProperty("ended_at")]
        public DateTimeOffset EndedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
