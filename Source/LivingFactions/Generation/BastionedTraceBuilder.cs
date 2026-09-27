using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace LivingFactions.Generation
{
    /// <summary>
    /// Traza italiana (Palmanova, Charleville, Pamplona) para facciones industriales o más:
    /// - Cortinas: muralla recta y lisa entre baluartes.
    /// - Baluartes en punta en las esquinas: sus flancos disparan a lo largo de las cortinas (sin zonas muertas).
    /// - Portones con revellín delante: para llegar a la puerta hay que rodearlo bajo fuego cruzado.
    /// - Foso de agua poco profunda (ralentiza), interrumpido frente a los portones.
    /// - Glacis: terreno despejado fuera de la fortificación.
    /// Las piezas (baluartes, portones) son FortPieceDef dibujadas en XML.
    /// </summary>
    public static class BastionedTraceBuilder
    {
        private const int GateInset = 5;
        private const int MoatInner = 3;       // distancia a la muralla donde empieza el foso
        private const int MoatOuter = 5;       // y donde termina (foso de 3 casillas)
        private const int GateMoatGap = 4;     // sin foso alrededor de portón y revellín
        private const int GlacisWidth = 14;

        public static void Build(Map map, CellRect rect, Faction faction, TierData data)
        {
            FortPieceDef bastion = DefDatabase<FortPieceDef>.GetNamedSilentFail(data.bastionPiece);
            FortPieceDef gate = DefDatabase<FortPieceDef>.GetNamedSilentFail(data.gatePiece);
            if (bastion == null || gate == null)
            {
                Log.Warning($"[Living Factions] Faltan las piezas {data.bastionPiece}/{data.gatePiece}; se usa la muralla simple.");
                OuterWallBuilder.Build(map, rect, faction, data);
                return;
            }

            ThingDef wallStuff = FortBuildUtility.WallStuffFor(faction);
            FortFootprint footprint = new FortFootprint();
            foreach (IntVec3 c in rect)
            {
                footprint.interior.Add(c);
            }

            // 1. Cortinas en todo el borde; los baluartes y portones se colocan encima.
            foreach (IntVec3 c in rect.EdgeCells)
            {
                if (c.InBounds(map) && (FortBuildUtility.TrySpawn(map, ThingDefOf.Wall, wallStuff, c, Rot4.North, faction) || FortBuildUtility.IsNaturalRock(map, c)))
                {
                    footprint.structure.Add(c);
                }
            }

            // 2. Baluartes en las esquinas (dibujados para el noreste; horario: NE, SE, SO, NO).
            Queue<ThingDef> heavy = HeavyTurretQueue(data);
            IntVec3[] corners =
            {
                new IntVec3(rect.maxX, 0, rect.maxZ),
                new IntVec3(rect.maxX, 0, rect.minZ),
                new IntVec3(rect.minX, 0, rect.minZ),
                new IntVec3(rect.minX, 0, rect.maxZ)
            };
            for (int i = 0; i < corners.Length; i++)
            {
                FortPieceStamper.Stamp(map, bastion, corners[i], i, faction, wallStuff, heavy, footprint);
            }

            // 3. Portones con revellín en el centro de los lados (dibujados para el norte; horario: N, E, S, O).
            IntVec3[] sideMids =
            {
                new IntVec3(rect.CenterCell.x, 0, rect.maxZ),
                new IntVec3(rect.maxX, 0, rect.CenterCell.z),
                new IntVec3(rect.CenterCell.x, 0, rect.minZ),
                new IntVec3(rect.minX, 0, rect.CenterCell.z)
            };
            IntVec3[] outward = { IntVec3.North, IntVec3.East, IntVec3.South, IntVec3.West };
            List<int> gateSides = data.gates >= 4
                ? new List<int> { 0, 1, 2, 3 }
                : (Rand.Bool ? new List<int> { 0, 2 } : new List<int> { 1, 3 }).Take(data.gates).ToList();
            List<IntVec3> gateInsides = new List<IntVec3>();
            HashSet<IntVec3> gateZones = new HashSet<IntVec3>();
            foreach (int side in gateSides)
            {
                FortFootprint gateFootprint = new FortFootprint();
                FortPieceStamper.Stamp(map, gate, sideMids[side], side, faction, wallStuff, heavy, gateFootprint);
                footprint.structure.UnionWith(gateFootprint.structure);
                footprint.interior.UnionWith(gateFootprint.interior);
                footprint.miniTurrets += gateFootprint.miniTurrets;
                gateInsides.Add(sideMids[side] - outward[side] * GateInset);
                // Zona sin foso: la pieza del portón ampliada, para que haya paso en tierra firme.
                foreach (IntVec3 c in gateFootprint.structure.Concat(gateFootprint.interior))
                {
                    foreach (IntVec3 z in CellRect.CenteredOn(c, GateMoatGap))
                    {
                        gateZones.Add(z);
                    }
                }
            }

            // 4. Foso de agua poco profunda y 5. glacis.
            int moatCells = DigMoat(map, rect, footprint, gateZones);
            CellRect glacis = rect.ExpandedBy(GlacisWidth + 12);
            FortBuildUtility.ClearFiringZone(map, glacis, c => footprint.interior.Contains(c) || footprint.structure.Contains(c));

            MapGenerator.SetVar(OuterWallBuilder.GateInsidesVar, gateInsides);

            if (Prefs.DevMode)
            {
                Log.Message($"[Living Factions] Traza italiana: 4 baluartes ({bastion.defName}), {gateSides.Count} portones con revellín, " +
                    $"{footprint.miniTurrets} mini torretas, {footprint.heavyTurrets} pesadas, foso de {moatCells} casillas, material {wallStuff?.defName}.");
            }
        }

        private static Queue<ThingDef> HeavyTurretQueue(TierData data)
        {
            ThingDef autocannon = DefDatabase<ThingDef>.GetNamedSilentFail("Turret_Autocannon");
            ThingDef sniper = DefDatabase<ThingDef>.GetNamedSilentFail("Turret_Sniper");
            Queue<ThingDef> queue = new Queue<ThingDef>();
            int a = data.autocannons;
            int s = data.sniperTurrets;
            while (a > 0 || s > 0)
            {
                if (a-- > 0 && autocannon != null)
                {
                    queue.Enqueue(autocannon);
                }
                if (s-- > 0 && sniper != null)
                {
                    queue.Enqueue(sniper);
                }
            }
            return queue;
        }

        /// <summary>
        /// Foso: casillas exteriores a entre MoatInner y MoatOuter de la fortificación (distancia en pasos de 8
        /// direcciones), salvo frente a los portones. Solo sobre terreno natural sin edificios ni roca.
        /// </summary>
        private static int DigMoat(Map map, CellRect rect, FortFootprint footprint, HashSet<IntVec3> gateZones)
        {
            CellRect area = rect.ExpandedBy(MoatOuter + 14).ClipInsideMap(map);
            Dictionary<IntVec3, int> dist = new Dictionary<IntVec3, int>();
            Queue<IntVec3> queue = new Queue<IntVec3>();
            foreach (IntVec3 c in footprint.structure)
            {
                dist[c] = 0;
                queue.Enqueue(c);
            }
            int dug = 0;
            while (queue.Count > 0)
            {
                IntVec3 c = queue.Dequeue();
                int d = dist[c];
                if (d >= MoatOuter)
                {
                    continue;
                }
                foreach (IntVec3 dir in GenAdj.AdjacentCells)
                {
                    IntVec3 n = c + dir;
                    if (!area.Contains(n) || dist.ContainsKey(n) || footprint.interior.Contains(n) || footprint.structure.Contains(n))
                    {
                        continue;
                    }
                    dist[n] = d + 1;
                    queue.Enqueue(n);
                    if (d + 1 >= MoatInner && !gateZones.Contains(n) && CanBeMoat(map, n))
                    {
                        FortBuildUtility.ClearCell(map, n);
                        map.terrainGrid.SetTerrain(n, TerrainDefOf.WaterShallow);
                        dug++;
                    }
                }
            }
            return dug;
        }

        private static bool CanBeMoat(Map map, IntVec3 c)
        {
            if (c.GetEdifice(map) != null)
            {
                return false;
            }
            TerrainDef terrain = c.GetTerrain(map);
            return terrain.natural && !terrain.IsWater && !terrain.bridge && terrain.passability == Traversability.Standable;
        }
    }
}
