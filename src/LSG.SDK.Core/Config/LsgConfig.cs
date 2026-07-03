namespace LSG.SDK.Core.Config
{
    /// <summary>
    /// Configuración central del SDK-core. Una instancia por proceso del mod.
    /// No hardcodear valores: inyectar desde el archivo de configuración propio de cada
    /// juego/mod-loader (ej. BepInEx config, config.json de tModLoader, etc.).
    /// </summary>
    public sealed class LsgConfig
    {
        /// <summary>Base URL de LSG-Auth, ej. "https://lsg.diinf.usach.cl/lsg-auth"</summary>
        public string AuthBaseUrl { get; set; } = "https://lsg.diinf.usach.cl/lsg-auth";

        /// <summary>Base URL de LSG-Core-API, ej. "https://lsg.diinf.usach.cl/lsg-core-api"</summary>
        public string CoreApiBaseUrl { get; set; } = "https://lsg.diinf.usach.cl/lsg-core-api";

        /// <summary>id_videogame asignado en LSG para este mod (ver tabla videogame).</summary>
        public int GameId { get; set; }

        /// <summary>Versión del plugin/mod, reportada en connect/sessions para trazabilidad.</summary>
        public string PluginVersion { get; set; } = "0.1.0";

        /// <summary>
        /// Margen de seguridad antes de expirar el token (segundos) para disparar refresh
        /// proactivo. El token JWT dura 120 min (7200s); se refresca al quedar bajo este umbral.
        /// </summary>
        public int TokenRefreshThresholdSeconds { get; set; } = 300;

        /// <summary>Timeout HTTP por request.</summary>
        public int HttpTimeoutSeconds { get; set; } = 15;

        /// <summary>Intervalo de reintento de flush de la cola offline (segundos).</summary>
        public int OfflineFlushIntervalSeconds { get; set; } = 60;

        /// <summary>Tag de experimento FONDECYT para instrumentación (ej. "LSG_C1_T1_CV").</summary>
        public string? ExperimentTag { get; set; }
    }
}
