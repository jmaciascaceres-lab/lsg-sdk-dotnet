using BepInEx.Logging;
using Unity.Entities;

namespace VRisingLsgMod.Effects
{
    /// <summary>
    /// Acceso al World ECS del servidor de VRising. World/EntityManager son API
    /// ESTÁNDAR de Unity.Entities (Unity.Entities.World), no específicas de
    /// VRising — confirmado en dnSpy (2026-07-15): World.All, World.Name,
    /// World.EntityManager, World.GetExistingSystemManaged&lt;T&gt;() son públicos.
    /// </summary>
    internal static class VWorld
    {
        private static ManualLogSource? _log;

        public static void Init(ManualLogSource log) => _log = log;

        /// <summary>
        /// Busca el World llamado "Server" (patrón estándar documentado en la
        /// comunidad de modding de VRising — funciona tanto en servidor dedicado
        /// como en el servidor embebido de una partida en solitario). La
        /// primera vez que se llama, loguea TODOS los nombres de World.All como
        /// diagnóstico — mismo criterio de "instrumentar antes de asumir" que
        /// usamos con Update()/OnGUI en Raft/Valheim, por si "Server" no es el
        /// nombre exacto en esta versión del juego.
        /// </summary>
        private static bool _loggedWorldNames;

        public static World? GetServerWorld()
        {
            if (!_loggedWorldNames)
            {
                _loggedWorldNames = true;
                var names = new System.Collections.Generic.List<string>();
                foreach (var w in World.All)
                    names.Add(w.Name);
                _log?.LogInfo($"World.All disponibles: [{string.Join(", ", names)}]");
            }

            foreach (var world in World.All)
            {
                if (world.Name == "Server")
                    return world;
            }

            _log?.LogWarning("No se encontró un World llamado \"Server\" — ver el log de World.All de arriba para el nombre real.");
            return null;
        }

        /// <summary>
        /// PENDIENTE (2026-07-16): cómo obtener la Entity del jugador local/objetivo
        /// no se ha confirmado todavía contra el dump de VRising — a diferencia de
        /// Valheim, que tenía Player.m_localPlayer como campo estático directo, no
        /// vimos un equivalente tan simple para VRising. KindredCommands (mod de
        /// referencia externo) resuelve esto vía su propio sistema de comandos de
        /// chat (recibe la Entity como parte del comando), no por una vía estática
        /// simple que podamos copiar 1:1 para un flujo de canje automático.
        ///
        /// Candidatos a investigar en dnSpy antes de implementar esto:
        ///   - Un componente tipo "PlayerCharacter"/"User" con un EntityQuery que
        ///     lo recorra completo (útil para un mod de servidor que aplica a
        ///     todos los jugadores conectados, no a "un" jugador local).
        ///   - Si LSG opera esto desde el lado del CLIENTE (como Raft/Valheim) en
        ///     vez del servidor, puede no ser viable en absoluto para VRising,
        ///     dado que DebugEventsSystem/BuffUtility viven en el World "Server".
        ///
        /// Retorna null explícitamente mientras esto no se resuelva — cualquier
        /// llamador debe manejar el caso null sin asumir que esto ya funciona.
        /// </summary>
        public static Entity? TryGetTargetPlayerEntity(World serverWorld, int lsgPlayerId)
        {
            _log?.LogWarning(
                "TryGetTargetPlayerEntity: PENDIENTE de implementar — falta confirmar en dnSpy " +
                "cómo mapear un player_id de LSG a una Entity de jugador en VRising.");
            return null;
        }
    }
}
