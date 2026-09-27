using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LivingFactions.AI
{
    [DefOf]
    public static class LF_DutyDefOf
    {
        public static DutyDef LF_HoldPost;

        static LF_DutyDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(LF_DutyDefOf));
        }
    }

    /// <summary>Como LordToil_DefendPoint, pero con la orden LF_HoldPost (que incluye comer raciones).</summary>
    public class LordToil_LFHoldPost : LordToil_DefendPoint
    {
        public LordToil_LFHoldPost(IntVec3 point, float defendRadius, float wanderRadius) : base(point, defendRadius, wanderRadius)
        {
        }

        public override void UpdateAllDuties()
        {
            LordToilData_DefendPoint data = Data;
            foreach (Pawn pawn in lord.ownedPawns)
            {
                if (pawn?.mindState == null)
                {
                    continue;
                }
                pawn.mindState.duty = new PawnDuty(LF_DutyDefOf.LF_HoldPost, data.defendPoint)
                {
                    focusSecond = data.defendPoint,
                    radius = pawn.kindDef.defendPointRadius >= 0f ? pawn.kindDef.defendPointRadius : data.defendRadius,
                    wanderRadius = data.wanderRadius
                };
            }
        }
    }

    /// <summary>
    /// Puesto del perímetro. Mantiene la posición, pero sale a atacar si:
    /// hieren a alguien del puesto, el jugador daña edificios de la base, el puesto pierde a un tercio,
    /// pasa hambre urgente (se acabaron las raciones) o el asedio se alarga.
    /// Nunca huye (defiende su propia base).
    /// </summary>
    public class LordJob_LFPerimeterPost : LordJob
    {
        private const int SiegeTicksBeforeSally = 2 * GenDate.TicksPerDay;

        private IntVec3 point;
        private float defendRadius;
        private float wanderRadius;

        public LordJob_LFPerimeterPost()
        {
        }

        public LordJob_LFPerimeterPost(IntVec3 point, float defendRadius, float wanderRadius)
        {
            this.point = point;
            this.defendRadius = defendRadius;
            this.wanderRadius = wanderRadius;
        }

        public override bool AddFleeToil => false;

        public override StateGraph CreateGraph()
        {
            StateGraph graph = new StateGraph();
            LordToil_LFHoldPost hold = new LordToil_LFHoldPost(point, defendRadius, wanderRadius);
            graph.StartingToil = hold;
            LordToil_AssaultColony assault = new LordToil_AssaultColony(attackDownedIfStarving: true);
            graph.AddToil(assault);

            Transition sally = new Transition(hold, assault);
            sally.AddTrigger(new Trigger_PawnHarmed(0.5f));
            sally.AddTrigger(new Trigger_ChanceOnPlayerHarmNPCBuilding(0.3f));
            sally.AddTrigger(new Trigger_FractionPawnsLost(0.34f));
            sally.AddTrigger(new Trigger_LFGarrisonStarving());
            sally.AddTrigger(new Trigger_TicksPassed(SiegeTicksBeforeSally));
            sally.AddPostAction(new TransitionAction_WakeAll());
            graph.AddTransition(sally);
            return graph;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref point, "point");
            Scribe_Values.Look(ref defendRadius, "defendRadius");
            Scribe_Values.Look(ref wanderRadius, "wanderRadius");
        }
    }
}
