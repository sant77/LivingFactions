using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace LivingFactions.Generation
{
    /// <summary>
    /// Defensas de las ciudades y capitales tribales, además de los anillos:
    /// - Trincheras: línea de barricadas detrás de cada anillo (con huecos para moverse).
    /// - Búnkeres (solo empalizada): salas de piedra con techo grueso junto al anillo interior. Un mortero que
    ///   cae sobre techo grueso se destruye sin daño (Projectile.ImpactSomething): refugio contra el bombardeo,
    ///   como los pihareinga de Ruapekapeka.
    /// - Trampas de púas en los accesos, sobre todo frente a las entradas del anillo exterior.
    /// - Puntos de emboscada fuera del anillo exterior, con cobertura (árboles o roca). Los guerreros aparecen
    ///   cuando un colono se acerca (MapComponent_SettlementInfo).
    /// </summary>
    public static class TribalDefenses
    {
        public const string BlockedCellsVar = "LF_TribalBlocked";

        private const int TrenchGapModulo = 9;
        private const int BunkerSize = 5;
        private const int TrapBandMin = 2;
        private const int TrapBandMax = 8;
        private const int AmbushBandMin = 6;
        private const int AmbushBandMax = 14;
        private const int AmbushSpacing = 15;

        public static void Build(Map map, CellRect rect, Faction faction, TierData data, TribalFort fort, ThingDef stuff,
            List<HashSet<IntVec3>> ringOpenings, out List<IntVec3> ambushPoints)
        {
            HashSet<IntVec3> blocked = new HashSet<IntVec3>();
            int rings = ringOpenings.Count;
            int barricades = 0;

            // Trincheras detrás de cada anillo.
            for (int i = 0; i < rings; i++)
            {
                Ellipse trench = new Ellipse(rect, i * TierData.TribalRingSpacing + 1.5f);
                foreach (IntVec3 c in TribalFortBuilder.RingCells(trench))
                {
                    bool nearOpening = ringOpenings[i].Any(o => o.DistanceToSquared(c) <= 9);
                    bool gap = (c.x * 7 + c.z * 13) % TrenchGapModulo == 0;
                    if (!nearOpening && !gap && c.InBounds(map) && c.GetEdifice(map) == null
                        && FortBuildUtility.TrySpawn(map, ThingDefOf.Barricade, stuff, c, Rot4.North, faction))
                    {
                        barricades++;
                    }
                }
            }

            // Búnkeres junto al anillo interior (solo empalizada).
            int bunkers = 0;
            if (fort == TribalFort.Palisade && data.tribalBunkers > 0)
            {
                ThingDef stone = DefDatabase<ThingDef>.GetNamedSilentFail("Blocks" + FortBuildUtility.LocalStoneName(map)) ?? stuff;
                Ellipse inner = new Ellipse(rect, (rings - 1) * TierData.TribalRingSpacing + 5);
                float step = 360f / data.tribalBunkers;
                for (int b = 0; b < data.tribalBunkers; b++)
                {
                    IntVec3 center = inner.PointAt(135f + b * step);
                    if (BuildBunker(map, center, inner.Center, faction, stone, blocked))
                    {
                        bunkers++;
                    }
                }
            }
            MapGenerator.SetVar(BlockedCellsVar, blocked);

            // Trampas: primero frente a las entradas del anillo exterior, después al azar en la franja exterior.
            int traps = 0;
            Ellipse outer = new Ellipse(rect, 0);
            Vector3 mid = new Vector3(outer.cx, 0f, outer.cz);
            foreach (IntVec3 opening in ringOpenings[0])
            {
                Vector3 dir = (opening.ToVector3() - mid).normalized;
                for (int k = 3; k <= 7 && traps < data.tribalTraps; k += 2)
                {
                    IntVec3 c = opening + new IntVec3(Mathf.RoundToInt(dir.x * k + Rand.Range(-1f, 1f)), 0, Mathf.RoundToInt(dir.z * k + Rand.Range(-1f, 1f)));
                    if (TryTrap(map, c, faction, stuff))
                    {
                        traps++;
                    }
                }
            }
            Ellipse bandOuter = new Ellipse(rect, -TrapBandMax);
            Ellipse bandInner = new Ellipse(rect, -TrapBandMin);
            List<IntVec3> band = bandOuter.Bounds.Cells.Where(c => bandOuter.Contains(c) && !bandInner.Contains(c)).ToList();
            for (int tries = 0; tries < 200 && traps < data.tribalTraps && band.Count > 0; tries++)
            {
                if (TryTrap(map, band.RandomElement(), faction, stuff))
                {
                    traps++;
                }
            }

            // Puntos de emboscada con cobertura (árboles o roca) fuera del anillo exterior.
            ambushPoints = new List<IntVec3>();
            Ellipse ambushOuter = new Ellipse(rect, -AmbushBandMax);
            Ellipse ambushInner = new Ellipse(rect, -AmbushBandMin);
            List<IntVec3> candidates = ambushOuter.Bounds.Cells
                .Where(c => c.InBounds(map) && ambushOuter.Contains(c) && !ambushInner.Contains(c) && c.Standable(map))
                .InRandomOrder().Take(400).ToList();
            foreach (IntVec3 c in candidates.OrderByDescending(c => Cover(map, c)))
            {
                if (ambushPoints.Count >= data.tribalAmbushes)
                {
                    break;
                }
                if (ambushPoints.All(p => p.DistanceToSquared(c) >= AmbushSpacing * AmbushSpacing))
                {
                    ambushPoints.Add(c);
                }
            }

            if (Prefs.DevMode)
            {
                Log.Message($"[Living Factions] Defensas tribales: {barricades} barricadas de trinchera, {bunkers} búnkeres, {traps} trampas, {ambushPoints.Count} puntos de emboscada.");
            }
        }

        /// <summary>Sala de 5x5 (interior 3x3) con techo grueso y la puerta mirando al centro de la aldea.</summary>
        private static bool BuildBunker(Map map, IntVec3 center, IntVec3 villageCenter, Faction faction, ThingDef stone, HashSet<IntVec3> blocked)
        {
            CellRect rect = CellRect.CenteredOn(center, BunkerSize / 2);
            if (!rect.Cells.All(c => c.InBounds(map) && c.Standable(map) || FortBuildUtility.IsNaturalRock(map, c)))
            {
                return false;
            }
            IntVec3 toCenter = villageCenter - center;
            IntVec3 door = System.Math.Abs(toCenter.x) > System.Math.Abs(toCenter.z)
                ? new IntVec3(toCenter.x > 0 ? rect.maxX : rect.minX, 0, center.z)
                : new IntVec3(center.x, 0, toCenter.z > 0 ? rect.maxZ : rect.minZ);
            foreach (IntVec3 c in rect)
            {
                FortBuildUtility.ClearCell(map, c);
                blocked.Add(c);
                if (rect.IsOnEdge(c))
                {
                    if (c == door)
                    {
                        FortBuildUtility.TrySpawn(map, ThingDefOf.Door, stone, c, Rot4.North, faction);
                    }
                    else
                    {
                        FortBuildUtility.TrySpawn(map, ThingDefOf.Wall, stone, c, Rot4.North, faction);
                    }
                }
                else
                {
                    map.roofGrid.SetRoof(c, RoofDefOf.RoofRockThick);
                }
            }
            // Margen para que las chozas no la tapen.
            blocked.UnionWith(rect.ExpandedBy(2).Cells);
            return true;
        }

        private static bool TryTrap(Map map, IntVec3 c, Faction faction, ThingDef stuff)
        {
            if (!c.InBounds(map) || !c.Standable(map) || c.GetEdifice(map) != null || c.GetTerrain(map).IsWater)
            {
                return false;
            }
            return FortBuildUtility.TrySpawn(map, ThingDefOf.TrapSpike, stuff, c, Rot4.North, faction);
        }

        /// <summary>Cobertura alrededor: árboles y roca natural en un radio de 3.</summary>
        private static int Cover(Map map, IntVec3 c)
        {
            int cover = 0;
            foreach (IntVec3 n in GenRadial.RadialCellsAround(c, 3f, true))
            {
                if (!n.InBounds(map))
                {
                    continue;
                }
                if (FortBuildUtility.IsNaturalRock(map, n) || (n.GetPlant(map)?.def.plant.IsTree ?? false))
                {
                    cover++;
                }
            }
            return cover;
        }
    }
}
