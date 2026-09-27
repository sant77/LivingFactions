using RimWorld;
using Verse;
using Verse.AI;

namespace LivingFactions.AI
{
    /// <summary>
    /// Los defensores comen solo de sus raciones (inventario). A diferencia de JobGiver_GetFood, no buscan
    /// comida por el mapa: así no se comen el botín y, más adelante, la despensa central será su única
    /// otra fuente (un objetivo para el jugador en un asedio).
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
                return null;
            }
            float nutrition = FoodUtility.GetNutrition(pawn, ration, ration.def);
            Job job = JobMaker.MakeJob(JobDefOf.Ingest, ration);
            job.count = FoodUtility.WillIngestStackCountOf(pawn, ration.def, nutrition);
            return job;
        }

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_LFEatRation copy = (JobGiver_LFEatRation)base.DeepCopy(resolve);
            copy.minCategory = minCategory;
            return copy;
        }
    }
}
