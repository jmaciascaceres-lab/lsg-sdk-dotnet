namespace CoreKeeper.LSG.Mod.Effects.Patches
{
    /// <summary>
    /// Estado estático leído por el Harmony patch y escrito por
    /// CoreKeeperEffectInterpreter. Se mantiene deliberadamente simple (un solo
    /// multiplicador global) porque Core Keeper es single-player/co-op local
    /// pequeño — si en el futuro se soporta multi-jugador remoto real, esto debe
    /// indexarse por playerId en vez de ser un valor único de proceso.
    /// </summary>
    public static class MiningSpeedState
    {
        public const float DefaultMultiplier = 1.0f;

        public static float ActiveMultiplier { get; set; } = DefaultMultiplier;

        public static void Reset() => ActiveMultiplier = DefaultMultiplier;
    }
}
