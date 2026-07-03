using HarmonyLib;

namespace RaftLsgMod.Effects.Patches
{
    /// <summary>
    /// Intercepta Paddle.PaddlePaddle(Vector3, Vector3, float) para multiplicar la
    /// fuerza de remo mientras el buff "Paddle Speed Boost" (mmv=66) esté activo.
    ///
    /// POR QUÉ AQUÍ Y NO SOBRE EL CAMPO paddleForce:
    /// paddleForce es privado ([SerializeField] private float), pero PaddlePaddle es
    /// público y recibe la fuerza ya calculada como parámetro — un Prefix que
    /// modifica `ref float force` es más simple y no requiere reflexión sobre el campo.
    ///
    /// LIMITACIÓN CONOCIDA — DECISIÓN DE DISEÑO ACEPTADA (2026-07-03):
    /// Según el código decompilado de PaddlePaddle, la física del raft solo se
    /// actualiza cuando `Raft_Network.IsHost` es true:
    ///     if (Raft_Network.IsHost && this.raft != null && this.playerNetwork != null)
    ///         this.raft.AddForce(...)
    /// Esto significa que el buff SOLO tiene efecto físico visible si el jugador que
    /// canjeó los puntos es quien hostea su propia partida. En un cliente no-host, el
    /// canje se procesa igual en LSG (puntos debitados, ledger actualizado,
    /// interaction_logs registrado), pero el efecto en el juego no se manifiesta,
    /// porque Raft delega el cálculo de físicas exclusivamente al host.
    /// Aceptado como limitación de v1 porque el caso de uso típico (jugador
    /// hosteando su propia partida) es el mayoritario. Si en el futuro se requiere
    /// soporte para clientes no-host, la vía sería interceptar más arriba, en
    /// Paddle.OnPaddle(), antes del envío P2P — no explorado aún.
    ///
    /// ALCANCE POR JUGADOR:
    /// PaddlePaddle también se invoca en el host para acciones de remo de jugadores
    /// REMOTOS (recibidas por RPC). Se filtra por playerNetwork.IsLocalPlayer para
    /// que el buff solo afecte al jugador que está corriendo este mod, no a otros
    /// conectados a la misma partida.
    /// </summary>
    [HarmonyPatch(typeof(Paddle), nameof(Paddle.PaddlePaddle))]
    internal static class PaddleForcePatch
    {
        private static void Prefix(Paddle __instance, ref float force)
        {
            if (!PaddleSpeedBoostState.IsCurrentlyActive())
                return;

            var playerNetwork = Traverse.Create(__instance).Field("playerNetwork").GetValue<Network_Player>();
            if (playerNetwork is null || !playerNetwork.IsLocalPlayer)
                return;

            force *= PaddleSpeedBoostState.Multiplier;
        }
    }
}
