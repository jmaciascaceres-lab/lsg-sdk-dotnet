namespace StardewValleyLsgMod
{
    /// <summary>
    /// SMAPI serializa esta clase a config.json automáticamente vía
    /// helper.ReadConfig&lt;T&gt;()/WriteConfig - no hace falta el patrón
    /// ConfigEntry&lt;T&gt; de BepInEx.
    /// </summary>
    internal sealed class ModConfig
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public bool AutoLoginOnStart { get; set; } = true;

        // Speed Buff (mmv=77) - el catálogo real de LSG NO trae magnitud ni
        // duración en options, así que van acá.
        public int SpeedBuffAttributeId { get; set; } = 1;
        public int SpeedBuffCostAmount { get; set; } = 2; // calza con cost_amount del catálogo real
        public float SpeedBuffAmount { get; set; } = 2f;
        public int SpeedBuffDurationSeconds { get; set; } = 180;

        // Mining XP (mmv=84) - instantáneo, sin duración.
        public int MiningXpAttributeId { get; set; } = 1;
        public int MiningXpCostAmount { get; set; } = 2; // calza con cost_amount del catálogo real
        public int MiningXpAmount { get; set; } = 50;
    }
}
