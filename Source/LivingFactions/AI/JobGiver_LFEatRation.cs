using RimWorld;
using Verse;
using Verse.AI;

namespace LivingFactions.AI
{
    /// <summary>
    /// Los defensores comen de sus raciones (inventario) y, cuando se acaban, de la despensa de la ciudadela.
    /// A diferencia de JobGiver_GetFood, no buscan comida por el mapa: no se comen el botín, y la despensa es
    /// un objetivo para el jugador en un asedio (sin ella, el hambre los obliga a salir a pelear).
    /// </summary>
    public class JobGiver_LFEatRation : ThinkNode_JobGiver
    {
        public HungerCategory minCategory = HungerCategory.Hungry;

        protected override Job TryGiveJob(Pawn pawn)
        {
            Need_Food food = pawn.needs?.food;
            if (food == null || food.CurCategory < minCategory || pawn.inventory == null)
            {
                return null;
            }
            Thing ration = null;
            foreach (Thing thing in pawn.inventory.innerContainer)
            {
                if (thing.def.IsNutritionGivingIngestible && thing.IngestibleNow && pawn.WillEat(thing) && pawn.RaceProps.CanEverEat(thing))
                {
                    ration = thing;
                    break;
                }
            }
            if (ration == null)
            {
                ration = FindPantryFood(pawn);
            }
            if (ration == null)
            {
                return null;
            }
            float nutrition = FoodUtility.GetNutrition(pawn, ration, ration.def);
            Job job = JobMaker.MakeJob(JobDefOf.Ingest, ration);
            job.count = FoodUtility.WillIngestStackCountOf(pawn, ration.def, nutrition);
            return job;
        }

        /// <summary>Sin raciones: la comida más cercana de la despensa de la ciudadela (y de ningún otro sitio).</summary>
        private static Thing FindPantryFood(Pawn pawn)
        {
            MapComponent_SettlementInfo info = pawn.Map?.GetComponent<MapComponent_SettlementInfo>();
            if (info == null || info.pantryCells.NullOrEmpty())
            {
                return null;
            }
            Thing best = null;
            float bestDist = float.MaxValue;
            foreach (IntVec3 c in info.pantryCells)
            {
                foreach (Thing t in c.GetThingList(pawn.Map))
                {
                    if (t.def.category != ThingCategory.Item || !t.def.IsNutritionGivingIngestible || !t.IngestibleNow
                        || !pawn.WillEat(t) || !pawn.RaceProps.CanEverEat(t) || !pawn.CanReserve(t))
                    {
                        continue;
                    }
                    float dist = pawn.Position.DistanceToSquared(c);
                    if (dist < bestDist && pawn.CanReach(t, PathEndMode.ClosestTouch, Danger.Some))
                    {
                        best = t;
                        bestDist = dist;
                    }
                }
            }
            return best;
        }

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_LFEatRation copy = (JobGiver_LFEatRation)base.DeepCopy(resolve);
            copy.minCategory = minCategory;
            return copy;
        }
    }
}
