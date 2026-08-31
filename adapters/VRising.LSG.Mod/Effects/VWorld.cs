using BepInEx.Logging;
using ProjectM.Network;
using Unity.Entities;

namespace VRisingLsgMod.Effects
{
    /// <summary>
    /// Acceso al World ECS del servidor de VRising. World/EntityManager son API
    /// ESTANDAR de Unity.Entities (Unity.Entities.World), no específicas de
    /// VRising - confirmado en dnSpy (2026-07-15): World.All, World.Name,
    /// World.EntityManager, World.GetExistingSystemManaged&lt;T&gt;() son públicos.
    /// </summary>
    internal static class VWorld
    {
        private static ManualLogSource? _log;

        public static void Init(ManualLogSource log) => _log = log;

        /// <summary>
        /// Busca el World llamado "Server" (patrón estándar documentado en la
        /// comunidad de modding de VRising - funciona tanto en servidor dedicado
        /// como en el servidor embebido de una partida en solitario). La
        /// primera vez que se llama, loguea TODOS los nombres de World.All como
        /// diagnóstico - mismo criterio de "instrumentar antes de asumir" que
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

            // CONFIRMADO EN JUEGO (2026-07-17): World.All en el proceso del
            // CLIENTE (VRising.exe) solo trae ["Default World", "LoadingWorld0"]
            // - NO existe "Server" ahi. VRising, a diferencia de Valheim, corre
            // el servidor como un PROCESO SEPARADO (VRisingServer.exe) incluso
            // en partidas de un jugador. Este plugin debe instalarse en el
            // BepInEx de VRisingServer.exe, no en el de VRising.exe.
            // CONFIRMADO FUNCIONANDO (2026-07-17): World "Server" SI aparece
            // cuando el plugin corre dentro de VRisingServer.exe.
            _log?.LogWarning("No se encontro un World llamado \"Server\" en este proceso - probablemente este mod deba correr en VRisingServer.exe, no en VRising.exe. Ver nota en VWorld.GetServerWorld().");
            return null;
        }

        /// <summary>
        /// Resuelto (2026-07-17): ProjectM.Network.User : IComponentData tiene
        /// LocalCharacter (NetworkedEntity) + PlatformId (Steam ID) + IsConnected.
        /// NetworkedEntity.TryGetSyncedEntity(out Entity) resuelve la Entity real
        /// del personaje (estamos del lado del SERVIDOR).
        ///
        /// LIMITACIÓN v0.1, documentada a propósito: LSG no tiene el Steam ID
        /// del jugador almacenado en la base de datos todavía (confirmado
        /// 2026-07-17), así que no podemos hacer el match real
        /// player_id -> PlatformId -> User. Como atajo temporal, tomamos el
        /// PRIMER User con IsConnected == true - válido solo para pruebas con
        /// una única cuenta conectada a la vez. NO apto para producción con
        /// varios jugadores simultáneos - habría que:
        ///   (a) agregar steam_id a la tabla players de LSG, o
        ///   (b) pasar el PlatformId desde el propio mod (ej. leído de Steamworks
        ///       en el cliente) en vez de resolverlo server-side a ciegas.
        /// </summary>
        public static Entity? TryGetTargetPlayerEntity(World serverWorld, int lsgPlayerId)
        {
            var entityManager = serverWorld.EntityManager;

            // IL2CPP: CreateEntityQuery(ComponentType) SIGUE crasheando
            // (AccessViolationException nativo confirmado dos veces,
            // 2026-08-25) — incluso pasando un ComponentType suelto, el
            // compilador lo empaqueta en un Il2CppStructArray<ComponentType>
            // por detrás (es el único overload disponible en este binding),
            // cayendo en el mismo camino roto de marshaling de arrays.
            // Fix: EntityQueryBuilder.WithAll<T>() usa un método GENÉRICO
            // (mismo patrón que GetComponentData<User>(), que sí funciona sin
            // problema en este mismo log) — evita el marshaling de array por
            // completo.
            var builder = new Unity.Entities.EntityQueryBuilder(Unity.Collections.Allocator.Temp)
                .WithAll<User>();
            try
            {
                var query = builder.Build(entityManager);
                try
                {
                    var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
                    try
                    {
                        foreach (var userEntity in entities)
                        {
                            var user = entityManager.GetComponentData<User>(userEntity);
                            if (!user.IsConnected)
                                continue;

                            if (!user.LocalCharacter.TryGetSyncedEntity(out var characterEntity))
                            {
                                _log?.LogWarning($"User conectado (PlatformId={user.PlatformId}) sin LocalCharacter sincronizado todavía - saltando.");
                                continue;
                            }

                            _log?.LogWarning(
                                $"TryGetTargetPlayerEntity: usando atajo v0.1 (primer User conectado, PlatformId={user.PlatformId}) " +
                                $"en vez de un match real por player_id={lsgPlayerId} - LSG no tiene Steam ID almacenado todavía.");
                            return characterEntity;
                        }
                    }
                    finally
                    {
                        entities.Dispose();
                    }
                }
                finally
                {
                    query.Dispose();
                }
            }
            finally
            {
                builder.Dispose();
            }

            _log?.LogWarning("TryGetTargetPlayerEntity: no se encontró ningún User con IsConnected == true.");
            return null;
        }
    }
}
