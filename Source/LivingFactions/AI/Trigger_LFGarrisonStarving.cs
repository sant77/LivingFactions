using RimWorld;
using Verse;
using Verse.AI.Group;

namespace LivingFactions.AI
{
    /// <summary>
    /// Sustituye a Trigger_UrgentlyHungry en las guarniciones de ciudades y capitales. El vanilla hace salir a
    /// todo el grupo si un solo pawn tiene hambre urgente; este solo si la mayoría la tiene, es decir, cuando
    /// de verdad se acabaron las raciones y la despensa.
    /// </summary>
    public class Trigger_LFGarrisonStarving : Trigger
    {
        private const float Fraction = 0.5f;

        public override bool ActivateOn(Lord lord, TriggerSignal signal)
        {
            if (signal.type != TriggerSignalType.Tick || lord.ownedPawns.Count == 0 || Find.TickManager.TicksGame % 250 != 0)
            {
                return false;
            }
            int starving = 0;
            foreach (Pawn pawn in lord.ownedPawns)
            {
                Need_Food food = pawn.needs?.food;
                if (food != null && food.CurCategory >= HungerCategory.UrgentlyHungry)
                {
                    starving++;
                }
            }
            return starving > lord.ownedPawns.Count * Fraction;
        }
    }
}
