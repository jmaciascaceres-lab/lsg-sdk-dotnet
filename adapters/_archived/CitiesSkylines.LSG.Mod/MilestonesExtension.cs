using ICities;

namespace CitiesSkylinesLsgMod
{
    /// <summary>
    /// Confirmado en documentación oficial + decompile público de un mod real:
    /// el tipo del manager es "IMilestones" (namespace ICities), con
    /// UnlockMilestone(string name) y EnumerateMilestones() - mismo mecanismo
    /// detrás del cheat nativo "Unlock All" que trae el juego (Content
    /// Manager > Mods).
    ///
    /// Cada extensión recibe su manager vía OnCreated(), que el juego invoca
    /// automáticamente (no hace falta registro manual) - se guarda estático
    /// para que CitiesSkylinesEffectInterpreter pueda usarlo al canjear.
    /// </summary>
    public sealed class MilestonesExtension : MilestonesExtensionBase
    {
        internal static IMilestones? Manager { get; private set; }

        public override void OnCreated(IMilestones threading)
        {
            Manager = threading;
            base.OnCreated(threading);
        }

        public override void OnReleased()
        {
            Manager = null;
            base.OnReleased();
        }
    }
}
