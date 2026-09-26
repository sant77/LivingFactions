using System.Collections.Generic;
using RimWorld;
using Verse;

namespace LivingFactions.Generation
{
    /// <summary>
    /// Suelos y caminos del interior de ciudades y capitales, según el estilo de la facción:
    /// avenidas desde cada portón hasta una plaza central, y el resto del exterior pavimentado
    /// (salvo cultivos; los edificios conservan sus suelos; se retiran árboles y plantas silvestres). Las tribus no pavimentan: solo caminos de tierra apisonada.
    /// </summary>
    public static class CityPaver
    {
        private const int AvenueHalfWidth = 1;   // avenidas de 3 casillas
        private const int PlazaRadius = 5;

        public static void Pave(Map map, CellRect inner, Faction faction, IEnumerable<IntVec3> gateInsides)
        {
            FactionStyle style = FactionStyleUtility.StyleOf(faction);
            string localStone = LocalStoneName(map, inner.CenterCell);
            TerrainDef general;
            TerrainDef avenue;
            switch (style)
            {
                case FactionStyle.Tribal:
                    general = null;
                    avenue = Named("PackedDirt");
                    break;
                case FactionStyle.Pirate:
                    general = Named("Gravel");
                    avenue = Named("Concrete");
                    break;
                case FactionStyle.Empire:
                    general = Named("Tile" + localStone) ?? Named("TileSandstone");
                    avenue = Named("TileMarble") ?? Named("PavedTile");
                    break;
                default:
                    general = Named("Flagstone" + localStone) ?? Named("FlagstoneSandstone");
                    avenue = Named("PavedTile");
                    break;
            }

            // Avenidas y plaza.
            HashSet<IntVec3> avenueCells = new HashSet<IntVec3>();
            IntVec3 center = inner.CenterCell;
            foreach (IntVec3 gate in gateInsides)
            {
                foreach (IntVec3 c in GenSight.BresenhamCellsBetween(gate, center))
                {
                    foreach (IntVec3 w in CellRect.CenteredOn(c, AvenueHalfWidth))
                    {
                        avenueCells.Add(w);
                    }
                }
            }
            foreach (IntVec3 c in GenRadial.RadialCellsAround(center, PlazaRadius, true))
            {
                avenueCells.Add(c);
            }

            int paved = 0;
            foreach (IntVec3 c in inner)
            {
                TerrainDef floor = avenueCells.Contains(c) ? avenue : general;
                if (floor != null && c.InBounds(map) && CanPave(map, c))
                {
                    RemoveWildPlants(map, c);
                    map.terrainGrid.SetTerrain(c, floor);
                    paved++;
                }
            }
            if (Prefs.DevMode)
            {
                Log.Message($"[Living Factions] Suelos: {paved} casillas ({general?.defName ?? "tierra natural"}, avenidas {avenue?.defName}).");
            }
        }

        private static TerrainDef Named(string defName)
        {
            return DefDatabase<TerrainDef>.GetNamedSilentFail(defName);
        }

        /// <summary>Nombre de la piedra de la región (Granite, Marble, ...) para losas y baldosas.</summary>
        private static string LocalStoneName(Map map, IntVec3 cell)
        {
            TerrainDef rock = RimWorld.BaseGen.BaseGenUtility.RegionalRockTerrainDef(map.Tile, false);
            string name = rock?.defName ?? "";
            foreach (string stone in new[] { "Granite", "Limestone", "Marble", "Sandstone", "Slate" })
            {
                if (name.Contains(stone))
                {
                    return stone;
                }
            }
            return "Sandstone";
        }

        /// <summary>Solo terreno natural transitable, sin edificios ni cultivos.</summary>
        private static bool CanPave(Map map, IntVec3 c)
        {
            if (c.GetEdifice(map) != null)
            {
                return false;
            }
            TerrainDef terrain = c.GetTerrain(map);
            if (!terrain.natural || terrain.IsWater || terrain.passability != Traversability.Standable)
            {
                return false;
            }
            // Los cultivos se respetan; las plantas silvestres y los árboles se retiran.
            Plant plant = c.GetPlant(map);
            return plant == null || !IsCrop(plant);
        }

        private static bool IsCrop(Plant plant)
        {
            return plant.def.plant.Sowable && !plant.def.plant.IsTree;
        }

        private static void RemoveWildPlants(Map map, IntVec3 c)
        {
            Plant plant = c.GetPlant(map);
            if (plant != null && !IsCrop(plant))
            {
                plant.Destroy();
            }
        }
    }
}
