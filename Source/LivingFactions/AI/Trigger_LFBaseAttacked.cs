using RimWorld;
using Verse;
using Verse.AI.Group;

namespace LivingFactions.AI
{
    /// <summary>
    /// Como Trigger_ChanceOnPlayerHarmNPCBuilding, pero cuenta a cualquier enemigo de la facción (mecanoides,
    /// otras facciones, el jugador), no solo al jugador. Vanilla dejaba a los defensores mirando mientras otros
    /// enemigos destruían sus defensas.
    /// </summary>
    public class Trigger_LFBaseAttacked : Trigger
    {
        private readonly float chance;

        public Trigger_LFBaseAttacked(float chance)
        {
            this.chance = chance;
        }

        public override bool ActivateOn(Lord lord, TriggerSignal signal)
        {
            if (signal.type != TriggerSignalType.BuildingDamaged || signal.thing == null || signal.thing.def.category != ThingCategory.Building)
            {
                return false;
            }
            Thing instigator = signal.dinfo.Instigator;
            return signal.thing.Faction == lord.faction && signal.dinfo.Def.ExternalViolenceFor(signal.thing)
                && instigator?.Faction != null && instigator.Faction.HostileTo(lord.faction) && Rand.Value < chance;
        }
    }
}
