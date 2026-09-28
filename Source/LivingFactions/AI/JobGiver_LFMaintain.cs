using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace LivingFactions.AI
{
    /// <summary>
    /// Técnicos de la reserva central: fuera de combate recargan generadores (combustible) y cañones de torretas
    /// y morteros (acero, cañones reforzados) usando la tarea de recarga vanilla, pero solo con lo que hay en el
    /// almacén de la ciudadela (sala de energía). Destruir o saquear el almacén deja las defensas sin mantenimiento.
    /// Solo un tercio de la reserva hace de técnico. Las bases sin ciudadela no tienen mantenimiento.
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
            if (info == null || !info.generated || info.depotCells.NullOrEmpty() || pawn.Faction != pawn.Map.ParentFaction)
            {
                return null;
            }
            // Primero lo que está más vacío.
            foreach (Building building in info.Refuelables.Where(b => b.Spawned).OrderBy(b => b.TryGetComp<CompRefuelable>().FuelPercentOfMax))
            {
                CompRefuelable comp = building.TryGetComp<CompRefuelable>();
                if (!NeedsRefuel(comp, building, pawn))
                {
                    continue;
                }
                Thing fuel = FindDepotFuel(pawn, comp, info);
                if (fuel != null)
                {
                    return JobMaker.MakeJob(JobDefOf.Refuel, building, fuel);
                }
            }
            return null;
        }

        /// <summary>Las mismas condiciones que RefuelWorkGiverUtility.CanRefuel, sin su búsqueda de combustible en todo el mapa.</summary>
        private static bool NeedsRefuel(CompRefuelable comp, Building building, Pawn pawn)
        {
            if (comp == null || comp.Props.atomicFueling || comp.IsFull || !comp.allowAutoRefuel || !comp.ShouldAutoRefuelNow)
            {
                return false;
            }
            if (comp.FuelPercentOfMax > 0f && !comp.Props.allowRefuelIfNotEmpty)
            {
                return false;
            }
            return building.Faction == pawn.Faction && pawn.CanReserve(building);
        }

        private static Thing FindDepotFuel(Pawn pawn, CompRefuelable comp, MapComponent_SettlementInfo info)
        {
            ThingFilter filter = comp.Props.fuelFilter;
            foreach (IntVec3 c in info.depotCells)
            {
                foreach (Thing t in c.GetThingList(pawn.Map))
                {
                    if (t.def.category == ThingCategory.Item && filter.Allows(t) && pawn.CanReserve(t)
                        && pawn.CanReach(t, PathEndMode.ClosestTouch, Danger.Some))
                    {
                        return t;
                    }
                }
            }
            return null;
        }
    }
}
