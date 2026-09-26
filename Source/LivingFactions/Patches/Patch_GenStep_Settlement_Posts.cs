using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace LivingFactions.Patches
{
    /// <summary>
    /// Defensa distribuida. En vanilla todos los defensores defienden el centro de la base y el
    /// perímetro queda vacío. Tras generar la base, parte de la guarnición pasa a puestos en los
    /// lados (LordJob_DefendPoint) y el resto queda como reserva central (LordJob_DefendBase).
    /// </summary>
    [HarmonyPatch(typeof(GenStep_Settlement), "ScatterAt")]
    public static class Patch_GenStep_Settlement_Posts
    {
        private const float PostWanderRadius = 5f;
        private const float PostDefendRadius = 14f;

        private static void Postfix(Map map)
        {
            SettlementTier? tier = WorldComponent_SettlementTiers.TierOfMap(map);
            if (!tier.HasValue)
            {
                return;
            }
            TierData data = TierData.For(tier.Value);
            if (data.perimeterPosts <= 0 || !MapGenerator.TryGetVar("SettlementRect", out CellRect rect))
            {
                return;
            }
            Faction faction = map.ParentFaction;
            Lord mainLord = map.lordManager.lords
                .Where(l => l.faction == faction && l.LordJob is LordJob_DefendBase)
                .MaxByWithFallback(l => l.ownedPawns.Count);
            if (mainLord == null)
            {
                return;
            }

            List<IntVec3> posts = PostPositions(map, rect, data).ToList();
            int toMove = (int)(mainLord.ownedPawns.Count * data.perimeterShare);
            if (posts.Count == 0 || toMove < posts.Count)
            {
                return;
            }

            List<Pawn> available = mainLord.ownedPawns.Where(p => p.RaceProps.Humanlike).ToList();
            int perPost = toMove / posts.Count;
            foreach (IntVec3 post in posts)
            {
                // Los más cercanos al puesto, para que no crucen toda la base.
                List<Pawn> squad = available.OrderBy(p => p.Position.DistanceToSquared(post)).Take(perPost).ToList();
                if (squad.Count == 0)
                {
                    break;
                }
                foreach (Pawn pawn in squad)
                {
                    mainLord.RemovePawn(pawn);
                    available.Remove(pawn);
                }
                LordMaker.MakeNewLord(faction, new LordJob_DefendPoint(post, PostWanderRadius, PostDefendRadius), map, squad);
            }

            if (Prefs.DevMode)
            {
                Log.Message($"[Living Factions] Defensa distribuida: {posts.Count} puestos de ~{perPost} pawns, reserva central de {mainLord.ownedPawns.Count}.");
            }
        }

        /// <summary>Punto medio de cada lado, un poco hacia dentro del perímetro defensivo.</summary>
        private static IEnumerable<IntVec3> PostPositions(Map map, CellRect rect, TierData data)
        {
            int inset = (data.edgeDefenseWidth ?? 2) + 3;
            CellRect inner = rect.ContractedBy(inset);
            if (inner.Width < 4 || inner.Height < 4)
            {
                yield break;
            }
            IntVec3 center = inner.CenterCell;
            List<IntVec3> sides = new List<IntVec3>
            {
                new IntVec3(center.x, 0, inner.maxZ),
                new IntVec3(center.x, 0, inner.minZ),
                new IntVec3(inner.maxX, 0, center.z),
                new IntVec3(inner.minX, 0, center.z)
            };
            // Con 2 puestos, dos lados opuestos elegidos al azar.
            IEnumerable<IntVec3> chosen = data.perimeterPosts >= 4
                ? sides
                : (Rand.Bool ? sides.Take(2) : sides.Skip(2).Take(2)).Take(data.perimeterPosts);
            foreach (IntVec3 side in chosen)
            {
                if (CellFinder.TryFindRandomCellNear(side, map, 5, c => c.Standable(map) && inner.Contains(c), out IntVec3 cell))
                {
                    yield return cell;
                }
            }
        }
    }
}
