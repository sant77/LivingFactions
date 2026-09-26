using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace LivingFactions.Generation
{
    /// <summary>Resultado de colocar piezas: qué casillas son fortificación y cuáles interior (para el foso).</summary>
    public class FortFootprint
    {
        public readonly HashSet<IntVec3> structure = new HashSet<IntVec3>();
        public readonly HashSet<IntVec3> interior = new HashSet<IntVec3>();
        public int miniTurrets;
        public int heavyTurrets;
    }

    /// <summary>
    /// Coloca una FortPieceDef girada en el mapa. Rotación en pasos de 90° en sentido horario: una pieza
    /// dibujada para el noreste (o el norte) pasa a sureste (este), suroeste (sur) y noroeste (oeste).
    /// </summary>
    public static class FortPieceStamper
    {
        public static IntVec3 Rotate(IntVec2 offset, int clockwiseSteps)
        {
            int x = offset.x;
            int z = offset.z;
            for (int i = 0; i < ((clockwiseSteps % 4) + 4) % 4; i++)
            {
                int oldX = x;
                x = z;
                z = -oldX;
            }
            return new IntVec3(x, 0, z);
        }

        public static void Stamp(Map map, FortPieceDef piece, IntVec3 anchor, int clockwiseSteps, Faction faction,
            ThingDef wallStuff, Queue<ThingDef> heavyTurrets, FortFootprint footprint)
        {
            bool hasTurrets = faction != null && faction.def.techLevel >= TechLevel.Industrial;
            Dictionary<char, List<IntVec3>> byType = new Dictionary<char, List<IntVec3>>();
            foreach ((IntVec2 offset, char type) in piece.Cells)
            {
                IntVec3 cell = anchor + Rotate(offset, clockwiseSteps);
                if (!cell.InBounds(map))
                {
                    continue;
                }
                char t = type;
                if (!hasTurrets && (t == 'T' || t == 'H'))
                {
                    t = 'W';
                }
                if (!byType.TryGetValue(t, out List<IntVec3> list))
                {
                    byType[t] = list = new List<IntVec3>();
                }
                list.Add(cell);
            }

            // Orden: despejar el interior, muros y sacos, agua, torretas y por último puertas.
            foreach (IntVec3 c in Get(byType, '.'))
            {
                FortBuildUtility.ClearCell(map, c);
                footprint.interior.Add(c);
            }
            foreach (IntVec3 c in Get(byType, 'W'))
            {
                if (FortBuildUtility.TrySpawn(map, ThingDefOf.Wall, wallStuff, c, Rot4.North, faction) || FortBuildUtility.IsNaturalRock(map, c))
                {
                    footprint.structure.Add(c);
                }
            }
            foreach (IntVec3 c in Get(byType, 'S'))
            {
                FortBuildUtility.TrySpawn(map, ThingDefOf.Sandbags, null, c, Rot4.North, faction);
                footprint.structure.Add(c);
            }
            foreach (IntVec3 c in Get(byType, '~'))
            {
                if (!FortBuildUtility.IsNaturalRock(map, c) && c.GetEdifice(map) == null)
                {
                    FortBuildUtility.ClearCell(map, c);
                    map.terrainGrid.SetTerrain(c, TerrainDefOf.WaterShallow);
                }
            }
            foreach (IntVec3 c in Get(byType, 'T'))
            {
                if (FortBuildUtility.TrySpawn(map, ThingDefOf.Turret_MiniTurret, null, c, Rot4.North, faction))
                {
                    footprint.miniTurrets++;
                }
                else
                {
                    FortBuildUtility.TrySpawn(map, ThingDefOf.Wall, wallStuff, c, Rot4.North, faction);
                }
                footprint.structure.Add(c);
            }
            foreach (CellRect block in Blocks2x2(Get(byType, 'H')))
            {
                ThingDef turret = heavyTurrets != null && heavyTurrets.Count > 0 ? heavyTurrets.Dequeue() : null;
                if (FortBuildUtility.TrySpawnOver(map, turret, null, block, faction))
                {
                    footprint.heavyTurrets++;
                }
                else
                {
                    // Sin torreta pesada disponible: una mini torreta en la esquina más exterior y muro en el resto.
                    IntVec3 outer = block.Cells.MaxBy(c => c.DistanceToSquared(anchor));
                    bool mini = hasTurrets && FortBuildUtility.TrySpawn(map, ThingDefOf.Turret_MiniTurret, null, outer, Rot4.North, faction);
                    if (mini)
                    {
                        footprint.miniTurrets++;
                    }
                    foreach (IntVec3 c in block)
                    {
                        if (!mini || c != outer)
                        {
                            FortBuildUtility.TrySpawn(map, ThingDefOf.Wall, wallStuff, c, Rot4.North, faction);
                        }
                    }
                }
                foreach (IntVec3 c in block)
                {
                    footprint.structure.Add(c);
                }
            }
            PlaceDoors(map, Get(byType, 'D'), faction, wallStuff, footprint);
        }

        private static IEnumerable<IntVec3> Get(Dictionary<char, List<IntVec3>> byType, char type)
        {
            return byType.TryGetValue(type, out List<IntVec3> list) ? list : Enumerable.Empty<IntVec3>();
        }

        /// <summary>Agrupa las casillas H en bloques 2x2 (esquina inferior izquierda primero).</summary>
        private static IEnumerable<CellRect> Blocks2x2(IEnumerable<IntVec3> cells)
        {
            HashSet<IntVec3> remaining = new HashSet<IntVec3>(cells);
            foreach (IntVec3 c in cells.OrderBy(c => c.x).ThenBy(c => c.z).ToList())
            {
                CellRect block = new CellRect(c.x, c.z, 2, 2);
                if (block.Cells.All(remaining.Contains))
                {
                    foreach (IntVec3 b in block)
                    {
                        remaining.Remove(b);
                    }
                    yield return block;
                }
            }
        }

        /// <summary>Dos casillas D contiguas: puerta de seguridad (Anomaly). Si no, puertas normales.</summary>
        private static void PlaceDoors(Map map, IEnumerable<IntVec3> doorCells, Faction faction, ThingDef wallStuff, FortFootprint footprint)
        {
            List<IntVec3> cells = doorCells.ToList();
            bool industrial = faction != null && faction.def.techLevel >= TechLevel.Industrial;
            ThingDef securityDoor = ModsConfig.AnomalyActive && industrial ? DefDatabase<ThingDef>.GetNamedSilentFail("SecurityDoor") : null;
            if (securityDoor != null && cells.Count == 2)
            {
                CellRect pair = CellRect.FromLimits(cells[0], cells[1]);
                if (pair.Area == 2 && FortBuildUtility.TrySpawnOver(map, securityDoor, null, pair, faction))
                {
                    footprint.structure.AddRange(cells);
                    return;
                }
            }
            foreach (IntVec3 c in cells)
            {
                FortBuildUtility.TrySpawn(map, ThingDefOf.Door, wallStuff, c, Rot4.North, faction);
                footprint.structure.Add(c);
            }
        }
    }
}
