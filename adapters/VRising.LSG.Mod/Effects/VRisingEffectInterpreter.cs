using BepInEx.Logging;
using LSG.SDK.Core.Mechanics;
using LSG.SDK.Core.Models;
using Newtonsoft.Json.Linq;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;

namespace VRisingLsgMod.Effects
{
    /// <summary>
    /// Traduce el catálogo de mecánicas de VRising a efectos reales. Confirmado
    /// en dnSpy sobre los ensamblados de interop de VRising (2026-07-14/15):
    ///
    ///   Movement Speed Boost -> DebugEventsSystem.ApplyBuff con un PrefabGUID
    ///     de un buff de velocidad ya existente en el juego (ej. -911970381,
    ///     "Voltatia's Electric Speed Buff" — confirmado como funcional por la
    ///     comunidad de modding, no verificado directo en nuestro dump ya que
    ///     los PrefabGUID son datos, no código), luego sobreescribir
    ///     ModifyMovementSpeedBuff.MoveSpeed en la Entity del buff resultante.
    ///     Patrón tomado de KindredCommands (Buffs.cs, open-source, AGPL-3.0) —
    ///     ADAPTADO, no copiado literal; los nombres de LifeTime/extensiones
    ///     Has/Add/Remove de esa referencia son de SU propio ECSExtensions.cs,
    ///     no del juego — acá se usa la API estándar de EntityManager en su lugar.
    ///
    ///   Blood Quality Insight -> escritura directa de Blood.Quality en la
    ///     Entity del jugador (IComponentData simple, sin necesidad de ningún
    ///     sistema de buffs).
    ///
    /// PENDIENTE: ambos métodos necesitan una Entity de jugador real —
    /// ver VWorld.TryGetTargetPlayerEntity (aún no implementado). Mientras
    /// tanto, Apply() falla de forma controlada y clara si no hay Entity.
    /// </summary>
    internal sealed class VRisingEffectInterpreter : ITimedEffectInterpreter
    {
        private const int MmvMovementSpeedBoost = 64;
        private const int MmvBloodQualityInsight = 65;

        // Voltatia's Electric Speed Buff — PrefabGUID reportado como funcional
        // por la comunidad de modding (hilo de Steam, no verificado contra nuestro
        // propio dump ya que es un dato de contenido, no código). Confirmar con
        // .listbuffs / KindredCommands si el valor cambia en una actualización del juego.
        private static readonly PrefabGUID VoltatiaSpeedBuffGuid = new(-911970381);

        private readonly ManualLogSource _log;

        public VRisingEffectInterpreter(ManualLogSource log)
        {
            _log = log;
            VWorld.Init(log);
        }

        public bool CanApply(MechanicDto mechanic) =>
            mechanic.MmvId == MmvMovementSpeedBoost || mechanic.MmvId == MmvBloodQualityInsight;

        public EffectApplicationResult Apply(MechanicDto mechanic)
        {
            var serverWorld = VWorld.GetServerWorld();
            if (serverWorld is null)
                return EffectApplicationResult.Failed("No se encontró el World \"Server\" — ver log de World.All.");

            // TODO: reemplazar 0 por el player_id real de LSG una vez que el
            // ciclo de canje pase ese dato hasta acá (Plugin.cs).
            var targetEntity = VWorld.TryGetTargetPlayerEntity(serverWorld, lsgPlayerId: 0);
            if (targetEntity is null)
            {
                return EffectApplicationResult.Failed(
                    "Resolución de Entity del jugador aún no implementada — ver VWorld.TryGetTargetPlayerEntity.");
            }

            return mechanic.MmvId switch
            {
                MmvMovementSpeedBoost => ApplyMovementSpeedBoost(serverWorld, targetEntity.Value, mechanic),
                MmvBloodQualityInsight => ApplyBloodQualityInsight(serverWorld, targetEntity.Value, mechanic),
                _ => EffectApplicationResult.Failed($"Mecánica no soportada por VRisingEffectInterpreter: mmv={mechanic.MmvId}"),
            };
        }

        public void Revert(TimedEffect effect)
        {
            // Movement Speed Boost: el buff spawneado tiene su propio LifeTime;
            // si se configuró con duración, VRising lo remueve solo. No hay
            // estado propio nuestro que revertir de este lado (a diferencia de
            // Raft/Valheim, que sí mantenían un "*State" estático).
            //
            // Blood Quality Insight: PENDIENTE decidir si se revierte al valor
            // original (habría que guardarlo en RevertState al aplicar) o se
            // deja que decaiga naturalmente por el propio sistema de sangre del
            // juego (Blood.LossPerSecond ya existe para eso).
        }

        private EffectApplicationResult ApplyMovementSpeedBoost(World serverWorld, Entity target, MechanicDto mechanic)
        {
            var entityManager = serverWorld.EntityManager;
            var moveSpeed = ReadFloat(mechanic, "move_speed", fallback: 6f);

            var debugEvents = serverWorld.GetExistingSystemManaged<DebugEventsSystem>();
            if (debugEvents is null)
                return EffectApplicationResult.Failed("No se encontró DebugEventsSystem en el World \"Server\".");

            var fromCharacter = new FromCharacter { User = target, Character = target };
            var buffEvent = new ApplyBuffDebugEvent { BuffPrefabGUID = VoltatiaSpeedBuffGuid };
            debugEvents.ApplyBuff(fromCharacter, buffEvent);

            if (!BuffUtility.TryGetBuff(target, VoltatiaSpeedBuffGuid,
                    entityManager.GetBufferLookup<BuffBuffer>(), out var buffEntity))
            {
                return EffectApplicationResult.Failed("El buff de velocidad no se aplicó (TryGetBuff devolvió false).");
            }

            if (entityManager.HasComponent<ModifyMovementSpeedBuff>(buffEntity))
            {
                var speedBuff = entityManager.GetComponentData<ModifyMovementSpeedBuff>(buffEntity);
                speedBuff.MoveSpeed = moveSpeed;
                entityManager.SetComponentData(buffEntity, speedBuff);
                _log.LogInfo($"Movement Speed Boost aplicado: MoveSpeed sobreescrito a {moveSpeed}.");
            }
            else
            {
                _log.LogWarning("El buff spawneado no tiene ModifyMovementSpeedBuff — se aplicó con su velocidad default, no la nuestra.");
            }

            return EffectApplicationResult.Ok();
        }

        private EffectApplicationResult ApplyBloodQualityInsight(World serverWorld, Entity target, MechanicDto mechanic)
        {
            var entityManager = serverWorld.EntityManager;
            var bonus = ReadFloat(mechanic, "quality_bonus", fallback: 10f);

            if (!entityManager.HasComponent<Blood>(target))
                return EffectApplicationResult.Failed("La Entity objetivo no tiene componente Blood.");

            var blood = entityManager.GetComponentData<Blood>(target);
            var original = blood.Quality;
            blood.Quality = System.Math.Clamp(blood.Quality + bonus, 0f, 100f);
            entityManager.SetComponentData(target, blood);

            _log.LogInfo($"Blood Quality Insight aplicado: Quality {original:F1} -> {blood.Quality:F1} (+{bonus}).");
            return EffectApplicationResult.Ok();
        }

        private static float ReadFloat(MechanicDto mechanic, string key, float fallback)
        {
            var value = mechanic.Options?[key]?.Value<float?>();
            return value ?? fallback;
        }
    }
}
