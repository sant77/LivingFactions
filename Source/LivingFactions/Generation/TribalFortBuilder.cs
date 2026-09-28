using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using RimWorld.Planet;
using Verse;

namespace LivingFactions.Generation
{
    public enum TribalFort
    {
        Palisade,   // empalizada (Ruapekapeka): madera, en bosque o llanura
        Pukara      // pukará (andino, mapuche): piedra local, en terreno rocoso o montañoso
    }

    /// <summary>Elipse (centro y radios) usada por los anillos tribales y la aldea orgánica.</summary>
    public struct Ellipse
    {
        public float cx;
        public float cz;
        public float rx;
        public float rz;

        public Ellipse(CellRect rect, float inset)
        {
            cx = (rect.minX + rect.maxX) / 2f;
            cz = (rect.minZ + rect.maxZ) / 2f;
            rx = rect.Width / 2f - inset - 0.5f;
            rz = rect.Height / 2f - inset - 0.5f;
        }

        public bool Contains(IntVec3 c)
        {
            float dx = (c.x - cx) / rx;
            float dz = (c.z - cz) / rz;
            return dx * dx + dz * dz <= 1f;
        }

        /// <summary>0 en el centro, 1 en el borde.</summary>
        public float Normalized(IntVec3 c)
        {
            float dx = (c.x - cx) / rx;
            float dz = (c.z - cz) / rz;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public IntVec3 Center => new IntVec3(Mathf.RoundToInt(cx), 0, Mathf.RoundToInt(cz));

        public IntVec3 PointAt(float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new IntVec3(Mathf.RoundToInt(cx + rx * Mathf.Cos(a)), 0, Mathf.RoundToInt(cz + rz * Mathf.Sin(a)));
        }

        public CellRect Bounds => CellRect.FromLimits(Mathf.FloorToInt(cx - rx) - 1, Mathf.FloorToInt(cz - rz) - 1, Mathf.CeilToInt(cx + rx) + 1, Mathf.CeilToInt(cz + rz) + 1);
    }

    /// <summary>
    /// Fortificación tribal: anillos elípticos concéntricos con entradas escalonadas (las aberturas de un anillo
    /// no se alinean con las del siguiente, como en los pukará: hay que rodear entre anillos bajo fuego).
    /// - Pukará: piedra local; la roca natural sirve de muro. Fuerte contra el fuego, débil contra la artillería.
    /// - Empalizada: madera. Fuerte contra el bombardeo (búnkeres, paso 2), débil contra el fuego.
    /// </summary>
    public static class TribalFortBuilder
    {
        public const string VillageEllipseVar = "LF_VillageEllipse";
        private const int GateInset = 3;
        private const float RockFractionForPukara = 0.08f;

        /// <summary>Pukará en terreno de colinas grandes o con roca alrededor; empalizada en los demás.</summary>
        public static TribalFort Choose(Map map, CellRect rect)
        {
            if (map.TileInfo.hilliness >= Hilliness.LargeHills)
            {
                return TribalFort.Pukara;
            }
            CellRect area = rect.ExpandedBy(10).ClipInsideMap(map);
            int samples = 0;
            int rock = 0;
            for (int x = area.minX; x <= area.maxX; x += 3)
            {
                for (int z = area.minZ; z <= area.maxZ; z += 3)
                {
                    samples++;
                    if (FortBuildUtility.IsNaturalRock(map, new IntVec3(x, 0, z)))
                    {
                        rock++;
                    }
                }
            }
            return samples > 0 && (float)rock / samples > RockFractionForPukara ? TribalFort.Pukara : TribalFort.Palisade;
        }

        /// <summary>Margen entre el borde de la base y la aldea (dentro del anillo interior).</summary>
        public static int VillageInset(TierData data)
        {
            return (Mathf.Max(1, data.tribalRings) - 1) * TierData.TribalRingSpacing + 2;
        }

        public static void Build(Map map, CellRect rect, Faction faction, TierData data, TribalFort fort)
        {
            ThingDef stuff = fort == TribalFort.Pukara
                ? DefDatabase<ThingDef>.GetNamedSilentFail("Blocks" + FortBuildUtility.LocalStoneName(map)) ?? ThingDefOf.WoodLog
                : ThingDefOf.WoodLog;
            int rings = Mathf.Max(1, data.tribalRings);
            int gates = Mathf.Max(2, data.gates);
            List<IntVec3> gateInsides = new List<IntVec3>();
            int walls = 0;

            for (int i = 0; i < rings; i++)
            {
                Ellipse ring = new Ellipse(rect, i * TierData.TribalRingSpacing);
                HashSet<IntVec3> cells = RingCells(ring);
                if (cells.Count == 0)
                {
                    continue;
                }

                // Entradas escalonadas: cada anillo gira media separación respecto al anterior.
                float step = 360f / gates;
                HashSet<IntVec3> openings = new HashSet<IntVec3>();
                for (int g = 0; g < gates; g++)
                {
                    float angle = 90f + g * step + (i % 2) * step / 2f;
                    IntVec3 target = ring.PointAt(angle);
                    IntVec3 first = cells.MinBy(c => c.DistanceToSquared(target));
                    openings.Add(first);
                    List<IntVec3> neighbours = cells.Where(c => c != first && c.AdjacentToCardinal(first)).ToList();
                    if (neighbours.Count > 0)
                    {
                        openings.Add(neighbours.MinBy(c => c.DistanceToSquared(target)));
                    }
                    if (i == rings - 1)
                    {
                        Vector3 toCenter = (ring.Center - first).ToVector3();
                        toCenter = toCenter.normalized * GateInset;
                        gateInsides.Add(first + new IntVec3(Mathf.RoundToInt(toCenter.x), 0, Mathf.RoundToInt(toCenter.z)));
                    }
                }

                foreach (IntVec3 c in cells)
                {
                    if (!c.InBounds(map))
                    {
                        continue;
                    }
                    if (openings.Contains(c))
                    {
                        FortBuildUtility.TrySpawn(map, ThingDefOf.Door, stuff, c, Rot4.North, faction);
                    }
                    else if (FortBuildUtility.TrySpawn(map, ThingDefOf.Wall, stuff, c, Rot4.North, faction))
                    {
                        walls++;
                    }
                }
            }

            // La aldea va dentro del anillo interior, con un margen.
            MapGenerator.SetVar(VillageEllipseVar, new Ellipse(rect, VillageInset(data)));
            MapGenerator.SetVar(OuterWallBuilder.GateInsidesVar, gateInsides);

            if (Prefs.DevMode)
            {
                Log.Message($"[Living Factions] {(fort == TribalFort.Pukara ? "Pukará" : "Empalizada")}: {rings} anillos, {gates} entradas por anillo (escalonadas), {walls} muros de {stuff.label}.");
            }
        }

        /// <summary>Borde de la elipse con conexión en cruz (sin huecos en diagonal).</summary>
        public static HashSet<IntVec3> RingCells(Ellipse e)
        {
            HashSet<IntVec3> ring = new HashSet<IntVec3>();
            foreach (IntVec3 c in e.Bounds)
            {
                if (e.Contains(c) && GenAdj.CardinalDirections.Any(d => !e.Contains(c + d)))
                {
                    ring.Add(c);
                }
            }
            return ring;
        }
    }
}
