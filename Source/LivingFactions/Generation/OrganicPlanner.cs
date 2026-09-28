using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.BaseGen;
using UnityEngine;
using Verse;

namespace LivingFactions.Generation
{
    /// <summary>
    /// Aldea orgánica para las ciudades y capitales tribales (en vez de la cuadrícula de distritos):
    /// - Centro: gran salón en la capital; plaza con fogata en la ciudad.
    /// - Caminos de tierra desde las entradas del anillo interior hasta el centro.
    /// - Chozas de tamaños variados dispersas dentro del anillo interior, sin tocar caminos ni el centro.
    ///   Las de la periferia pueden ser granjas.
    /// - Fogatas junto a los caminos.
    /// </summary>
    public static class OrganicPlanner
    {
        private const int CenterRadius = 12;        // gran salón: 23x23
        private const int PlazaRadius = 6;
        private const int HutGap = 2;
        private const int Attempts = 400;
        private const float CellsPerHut = 110f;
        private const float FarmRing = 0.62f;       // a partir de esta distancia normalizada puede haber granjas

        public static void Plan(ResolveParams rp, bool reserveCenter)
        {
            Map map = BaseGen.globalSettings.map;
            Faction faction = rp.faction ?? map.ParentFaction;
            if (!MapGenerator.TryGetVar(TribalFortBuilder.VillageEllipseVar, out Ellipse village))
            {
                village = new Ellipse(rp.rect, 1);
            }
            IntVec3 center = village.Center;

            // Centro.
            CellRect centerRect;
            if (reserveCenter)
            {
                centerRect = CellRect.CenteredOn(center, CenterRadius);
                MapGenerator.SetVar(DistrictPlanner.ReservedCenterVar, centerRect);
                CitadelBuilder.Build(map, centerRect, faction);
            }
            else
            {
                centerRect = CellRect.CenteredOn(center, PlazaRadius);
                MapGenerator.SetVar(DistrictPlanner.ReservedCenterVar, centerRect);
                FortBuildUtility.TrySpawn(map, ThingDefOf.Campfire, null, center, Rot4.North, faction);
            }

            // Caminos desde las entradas del anillo interior hasta el centro.
            HashSet<IntVec3> paths = new HashSet<IntVec3>();
            if (MapGenerator.TryGetVar(OuterWallBuilder.GateInsidesVar, out List<IntVec3> gates))
            {
                foreach (IntVec3 gate in gates)
                {
                    foreach (IntVec3 c in GenSight.BresenhamCellsBetween(gate, center))
                    {
                        paths.Add(c);
                        paths.Add(c + IntVec3.East);
                        paths.Add(c + IntVec3.North);
                    }
                }
            }
            MapGenerator.SetVar(DistrictPlanner.StreetCellsVar, paths);

            // Chozas.
            HashSet<IntVec3> blocked = new HashSet<IntVec3>(paths);
            blocked.UnionWith(centerRect.ExpandedBy(HutGap).Cells);
            int area = village.Bounds.Area;
            int wanted = Mathf.RoundToInt(area * Mathf.PI / 4f / CellsPerHut);
            IntRange hutSize = reserveCenter ? new IntRange(5, 11) : new IntRange(5, 9);
            List<(CellRect rect, DistrictType type)> huts = new List<(CellRect, DistrictType)>();
            for (int i = 0; i < Attempts && huts.Count < wanted; i++)
            {
                IntVec3 c = new IntVec3(Rand.RangeInclusive(village.Bounds.minX, village.Bounds.maxX), 0, Rand.RangeInclusive(village.Bounds.minZ, village.Bounds.maxZ));
                CellRect hut = new CellRect(c.x, c.z, hutSize.RandomInRange, hutSize.RandomInRange);
                if (!hut.Cells.All(x => village.Contains(x) && x.InBounds(map)) || hut.ExpandedBy(HutGap).Cells.Any(blocked.Contains)
                    || hut.Cells.Any(x => FortBuildUtility.IsNaturalRock(map, x)))
                {
                    continue;
                }
                bool periphery = village.Normalized(hut.CenterCell) > FarmRing;
                DistrictType type = periphery && Rand.Chance(0.45f) ? DistrictType.Farm : Rand.Chance(0.2f) ? DistrictType.Military : DistrictType.Civil;
                huts.Add((hut, type));
                blocked.UnionWith(hut.Cells);
            }

            BaseGen.globalSettings.minEmptyNodes = 0;
            foreach (DistrictType type in new[] { DistrictType.Farm, DistrictType.Civil, DistrictType.Military })
            {
                foreach ((CellRect hut, DistrictType _) in huts.Where(h => h.type == type))
                {
                    ResolveParams hutParams = rp;
                    hutParams.rect = hut;
                    BaseGen.symbolStack.Push(DistrictPlanner.SymbolForType(type), hutParams);
                }
            }

            // Fogatas junto a los caminos.
            int campfires = 0;
            foreach (IntVec3 c in paths.InRandomOrder())
            {
                if (campfires >= 4)
                {
                    break;
                }
                IntVec3 spot = c + GenAdj.CardinalDirections.RandomElement() * 2;
                if (village.Contains(spot) && !blocked.Contains(spot) && spot.InBounds(map) && spot.Standable(map)
                    && FortBuildUtility.TrySpawn(map, ThingDefOf.Campfire, null, spot, Rot4.North, faction))
                {
                    blocked.UnionWith(CellRect.CenteredOn(spot, 4).Cells);
                    campfires++;
                }
            }

            if (Prefs.DevMode)
            {
                Log.Message($"[Living Factions] Aldea orgánica: {huts.Count} chozas (civil {huts.Count(h => h.type == DistrictType.Civil)}, " +
                    $"guerreros {huts.Count(h => h.type == DistrictType.Military)}, granjas {huts.Count(h => h.type == DistrictType.Farm)}), " +
                    $"{campfires} fogatas, centro {(reserveCenter ? "gran salón" : "plaza")}.");
            }
        }
    }
}
