using System;
using ICities;
using LSG.SDK.Core.Api;
using LSG.SDK.Core.Auth;
using LSG.SDK.Core.Config;
using LSG.SDK.Core.Mechanics;
using LSG.SDK.Core.Models;
using LSG.SDK.Core.Offline;
using CitiesSkylinesLsgMod.Effects;

namespace CitiesSkylinesLsgMod
{
    /// <summary>
    /// IUserMod es la única interfaz obligatoria para que el juego reconozca
    /// el mod en el Content Manager. OnEnabled()/OnDisabled() NO son parte de
    /// la interfaz (no se declaran en IUserMod) pero el juego los invoca por
    /// reflexión si existen - patrón confirmado en la documentación oficial
    /// de modding, mismo mecanismo que usan las extensiones (IThreadingExtension,
    /// EconomyExtension, MilestonesExtension, etc.): el juego escanea el
    /// ensamblado buscando clases que implementen interfaces conocidas, sin
    /// necesitar un registro manual explícito.
    /// </summary>
    public sealed class Mod : IUserMod
    {
        // Confirmado: id_videogame = 14.
        private const int LsgGameId = 14;

        public string Name => "LSG Cities: Skylines Adapter";
        public string Description => "Adaptador LSG para Cities: Skylines vía la API oficial ICities.";

        internal static LsgConfig? LsgConfig { get; private set; }
        internal static LsgAuthClient? Auth { get; private set; }
        internal static LsgCoreApiClient? Api { get; private set; }
        internal static MechanicsCache? Mechanics { get; private set; }
        internal static OfflineQueue? OfflineQueue { get; private set; }
        internal static TimedEffectTracker? TimedEffects { get; private set; }
        internal static int? PlayerId { get; set; }
        internal static DateTimeOffset LastOfflineFlush { get; set; } = DateTimeOffset.UtcNow;

        private static readonly CitiesSkylinesEffectInterpreter Interpreter = new();

        // TODO: reemplazar antes de compilar (mismo patrón pragmático de
        // v0.1 que usamos en Terraria - sin sistema de config todavía).
        private const string LsgEmail = "TU_EMAIL";
        private const string LsgPassword = "TU_PASSWORD";

        // Sin sistema de comandos de consola/chat como SMAPI/tModLoader en
        // esta API - se dispara un canje de prueba automático tras el login,
        // mismo patrón que usamos al bootstrapear Raft originalmente.
        private const int TestAttributeId = 2;
        private const int TestAmount = 5000;

        public void OnEnabled()
        {
            LsgConfig = new LsgConfig { GameId = LsgGameId, PluginVersion = "0.1.0" };
            Auth = new LsgAuthClient(LsgConfig);
            Api = new LsgCoreApiClient(LsgConfig, Auth);
            Mechanics = new MechanicsCache(Api);
            OfflineQueue = new OfflineQueue(Api, LsgConfig);
            TimedEffects = new TimedEffectTracker();
            TimedEffects.OnExpired += effect =>
            {
                Interpreter.Revert(effect);
                UnityEngine.Debug.Log($"[LSG] Efecto expirado: {effect.Mechanic.Name} (mmv={effect.Mechanic.MmvId})");
            };

            UnityEngine.Debug.Log("[LSG] Cities: Skylines Adapter habilitado.");

            if (LsgEmail != "TU_EMAIL" && LsgPassword != "TU_PASSWORD")
            {
                _ = LoginAndInitializeAsync(LsgEmail, LsgPassword);
            }
            else
            {
                UnityEngine.Debug.LogWarning("[LSG] Credenciales no configuradas todavía - editar LsgEmail/LsgPassword en Mod.cs.");
            }
        }

        public void OnDisabled()
        {
            UnityEngine.Debug.Log("[LSG] Cities: Skylines Adapter deshabilitado.");
        }

        private async System.Threading.Tasks.Task LoginAndInitializeAsync(string email, string password)
        {
            try
            {
                var session = await Auth!.LoginAsync(email, password);
                PlayerId = session.Player.IdPlayers;
                UnityEngine.Debug.Log($"[LSG] Login OK - player_id={PlayerId}, roles=[{string.Join(",", session.Player.Roles)}]");

                await Mechanics!.RefreshAsync();
                UnityEngine.Debug.Log($"[LSG] Catálogo de mecánicas cargado: {Mechanics.All.Count} mecánica(s) para game_id={LsgGameId}.");

                UnityEngine.Debug.Log("[LSG] Prueba automática de canje programada en 8s (sin comandos de consola en esta API - ver nota en OnEnabled()).");
                _ = System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(8))
                    .ContinueWith(_ => _ = RedeemMechanicAsync(CitiesSkylinesEffectInterpreter.MmvCashIncomeBonus, TestAttributeId, TestAmount));
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LSG] Fallo en LoginAndInitializeAsync: {ex}");
            }
        }

        /// <summary>
        /// Ciclo de canje real: preview -> redeem -> aplicar efecto -> trackear
        /// expiración (si aplica). Mismo patrón que el resto de los adaptadores.
        /// </summary>
        internal static async System.Threading.Tasks.Task RedeemMechanicAsync(int mmvId, int attributeId, int amount)
        {
            if (!PlayerId.HasValue)
            {
                UnityEngine.Debug.LogError("[LSG] No hay sesión activa - revisa las credenciales en Mod.cs.");
                return;
            }

            try
            {
                var mechanic = Mechanics!.Get(mmvId);
                if (mechanic is null)
                {
                    UnityEngine.Debug.LogError($"[LSG] mmv={mmvId} no está en el catálogo cacheado.");
                    return;
                }

                var request = new RedeemRequestDto
                {
                    ModifiableMechanicVideogameId = mmvId,
                    AttributeId = attributeId,
                    Amount = amount,
                };

                var preview = await Api!.PreviewRedeemAsync(PlayerId.Value, request);
                if (preview is null || !preview.CanRedeem)
                {
                    UnityEngine.Debug.LogWarning($"[LSG] Saldo insuficiente para {mechanic.Name}: {preview?.CurrentBalance ?? -1} < {amount}.");
                    return;
                }

                var result = await Api.RedeemAsync(PlayerId.Value, request);
                UnityEngine.Debug.Log($"[LSG] Redeem OK ({mechanic.Name}): ledger_id={result?.PointsLedgerId}, saldo restante={result?.ResultingBalance}.");

                var effectResult = Interpreter.Apply(mechanic);
                if (!effectResult.Success)
                {
                    UnityEngine.Debug.LogError($"[LSG] Efecto no aplicado ({mechanic.Name}): {effectResult.Warning}");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LSG] Fallo en el ciclo de canje (mmv={mmvId}): {ex}");
            }
        }
    }
}
