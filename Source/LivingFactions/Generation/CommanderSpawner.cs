using System.Collections.Generic;
using System.Linq;
using LivingFactions.AI;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace LivingFactions.Generation
{
    /// <summary>
    /// Comandante de la capital: un pawn del tipo más fuerte de la facción, con el título en su apodo, y una
    /// guardia de élite. Defienden el salón del mando de la ciudadela y no salen por tiempo; solo si hieren a
    /// uno de ellos, si caen la mitad o si la mayoría pasa hambre. (El líder real de la facción queda para la Fase 3.)
    /// </summary>
    public static class CommanderSpawner
    {
        private const int GuardCount = 3;
        private const float HallDefendRadius = 8f;
        private const float HallWanderRadius = 3f;

        public static void Spawn(Map map, Faction faction, List<IntVec3> hallCells)
        {
            if (faction == null || hallCells.NullOrEmpty())
            {
                return;
            }
            List<PawnKindDef> elite = EliteKinds(faction);
            if (elite.Count == 0)
            {
                return;
            }
            IntVec3 center = hallCells.OrderBy(c => c.DistanceToSquared(Centroid(hallCells))).First();

            List<Pawn> group = new List<Pawn>();
            Pawn commander = Generate(elite[0], faction, map);
            if (commander.Name is NameTriple name)
            {
                commander.Name = new NameTriple(name.First, "LF_CommanderNick".Translate(name.Last).Resolve(), name.Last);
            }
            group.Add(commander);
            for (int i = 0; i < GuardCount; i++)
            {
                group.Add(Generate(elite[System.Math.Min(i + 1, elite.Count - 1)], faction, map));
            }

            foreach (Pawn pawn in group)
            {
                IntVec3 cell = CellFinder.RandomClosewalkCellNear(center, map, 3, c => hallCells.Contains(c));
                GenSpawn.Spawn(pawn, cell, map);
            }
            LordMaker.MakeNewLord(faction,
                new LordJob_LFPerimeterPost(center, HallDefendRadius, HallWanderRadius, siegeTicks: -1, harmChance: 1f, lostFraction: 0.5f),
                map, group);

            if (Prefs.DevMode)
            {
                Log.Message($"[Living Factions] Comandante: {commander.LabelShort} ({commander.kindDef.defName}) con {GuardCount} guardias ({string.Join(", ", group.Skip(1).Select(p => p.kindDef.defName))}).");
            }
        }

        /// <summary>Tipos de combate de la facción, del más fuerte al más débil.</summary>
        private static List<PawnKindDef> EliteKinds(Faction faction)
        {
            return faction.def.pawnGroupMakers?
                .Where(m => m.kindDef == PawnGroupKindDefOf.Combat || m.kindDef == PawnGroupKindDefOf.Settlement)
                .SelectMany(m => m.options)
                .Select(o => o.kind)
                .Where(k => k != null && k.RaceProps.Humanlike && !k.factionLeader)
                .Distinct()
                .OrderByDescending(k => k.combatPower)
                .ToList() ?? new List<PawnKindDef>();
        }

        private static Pawn Generate(PawnKindDef kind, Faction faction, Map map)
        {
            PawnGenerationRequest request = new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer, map.Tile,
                forceGenerateNewPawn: true, mustBeCapableOfViolence: true, inhabitant: true);
            Pawn pawn = PawnGenerator.GeneratePawn(request);
            if (pawn.needs?.food != null)
            {
                pawn.needs.food.CurLevel = pawn.needs.food.MaxLevel;
            }
            return pawn;
        }

        private static IntVec3 Centroid(List<IntVec3> cells)
        {
            return new IntVec3((int)cells.Average(c => c.x), 0, (int)cells.Average(c => c.z));
        }
    }
}
