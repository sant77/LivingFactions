using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace LivingFactions.AI
{
    /// <summary>
    /// Técnicos de la reserva central: fuera de combate recargan generadores (combustible) y cañones de
    /// torretas y morteros (acero, cañones reforzados), usando la tarea de recarga vanilla. Solo un tercio
    /// de la reserva hace de técnico, y solo en bases con rango.
    /// </summary>
    public class JobGiver_LFMaintain : ThinkNode_JobGiver
    {
        private const int TechnicianEvery = 3;

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.thingIDNumber % TechnicianEvery != 0 || pawn.Map == null || GenAI.InDangerousCombat(pawn))
            {
                return null;
            }
            MapComponent_SettlementInfo info = pawn.Map.GetComponent<MapComponent_SettlementInfo>();
            if (info == null || !info.generated || pawn.Faction != pawn.Map.ParentFaction)
            {
                return null;
            }
            // Primero lo que está más vacío.
            foreach (Building building in info.Refuelables.Where(b => b.Spawned).OrderBy(b => b.TryGetComp<CompRefuelable>().FuelPercentOfMax))
            {
                if (RefuelWorkGiverUtility.CanRefuel(pawn, building))
                {
                    return RefuelWorkGiverUtility.RefuelJob(pawn, building);
                }
            }
            return null;
        }
    }
}
