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
            string localStone = FortBuildUtility.LocalStoneName(map);
            TerrainDef general;
            TerrainDef avenue;
            TerrainDef indoor;
            switch (style)
            {
                case FactionStyle.Tribal:
                    general = null;
                    avenue = Named("PackedDirt");
                    indoor = Named("StrawMatting");
                    break;
                case FactionStyle.Pirate:
                    // La grava tiene casi el color de la tierra; el asfalto roto se nota y va con los piratas.
                    general = Named("BrokenAsphalt") ?? Named("Gravel");
                    avenue = Named("Concrete");
                    indoor = Named("Concrete");
                    break;
                case FactionStyle.Empire:
                    general = Named("Tile" + localStone) ?? Named("TileSandstone");
                    avenue = Named("TileMarble") ?? Named("PavedTile");
                    indoor = general;
                    break;
                default:
                    general = Named("Flagstone" + localStone) ?? Named("FlagstoneSandstone");
                    avenue = Named("PavedTile");
                    indoor = Named("WoodPlankFloor");
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
            // Calles de los distritos y centro reservado (plaza hasta que exista la ciudadela).
            if (MapGenerator.TryGetVar(DistrictPlanner.StreetCellsVar, out HashSet<IntVec3> streets))
            {
                avenueCells.UnionWith(streets);
            }
            if (MapGenerator.TryGetVar(DistrictPlanner.ReservedCenterVar, out CellRect reserved))
            {
                avenueCells.UnionWith(reserved.Cells);
            }

            int paved = 0;
            foreach (IntVec3 c in inner)
            {
                if (!c.InBounds(map))
                {
                    continue;
                }
                // Dentro de los edificios (techo construido, no natural): suelo de interior donde BaseGen dejó tierra.
                RoofDef cellRoof = c.GetRoof(map);
                bool indoors = cellRoof != null && !cellRoof.isNatural;
                TerrainDef floor = indoors ? indoor : avenueCells.Contains(c) ? avenue : general;
                if (floor != null && CanPave(map, c, indoors))
                {
                    RemoveWildPlants(map, c);
                    // Si hay un puente (cimiento en 1.6), el suelo se pone encima y el puente lo sostiene.
                    map.terrainGrid.SetTerrain(c, floor);
                    paved++;
                }
            }
            if (Prefs.DevMode)
            {
                Log.Message($"[Living Factions] Suelos: {paved} casillas ({general?.defName ?? "tierra natural"}, avenidas {avenue?.defName}, interiores {indoor?.defName}).");
            }
        }

        private static TerrainDef Named(string defName)
        {
            return DefDatabase<TerrainDef>.GetNamedSilentFail(defName);
        }

        /// <summary>
        /// Exterior sin edificios ni cultivos: terreno natural (salvo agua profunda o intransitable), las calles
        /// que pone BaseGen y los puentes sobre pantano o barro. Así queda un solo material general.
        /// Bajo los edificios no se toca: conservan sus suelos.
        /// </summary>
        private static bool CanPave(Map map, IntVec3 c, bool indoors)
        {
            RoofDef roof = c.GetRoof(map);
            if (c.GetEdifice(map) != null || (roof != null && roof.isNatural))
            {
                return false;
            }
            TerrainDef terrain = c.GetTerrain(map);
            // Dentro, solo donde no hay suelo construido (las habitaciones con suelo lo conservan).
            if (indoors && !terrain.natural)
            {
                return false;
            }
            if (terrain.passability == Traversability.Impassable)
            {
                return false;
            }
            // Agua sin puente debajo: no se pavimenta.
            if (terrain.IsWater && map.terrainGrid.FoundationAt(c) == null)
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
