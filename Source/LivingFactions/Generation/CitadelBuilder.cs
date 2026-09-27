using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace LivingFactions.Generation
{
    /// <summary>
    /// Ciudadela pentagonal en el centro de la capital (plantilla LF_Citadel en Defs/FortPieceDefs). Coloca la plantilla y
    /// amuebla cada sala según la facción:
    /// - h: salón del mando (trono del Imperio con Royalty; mesa y sillas en los demás).
    /// - p: despensa con comida cruda o pemmican (fuente de comida además de las raciones).
    /// - j: prisión: camas de prisionero, una por cada ~6 casillas (para los colonos capturados, Fase 3).
    /// - e: energía: generadores con combustible, baterías, y reservas de combustible y acero para el
    ///   mantenimiento. En tribus (sin electricidad) es un almacén.
    /// </summary>
    public static class CitadelBuilder
    {
        public const string PieceName = "LF_Citadel";
        private const float PantryNutrition = 100f;

        public static void Build(Map map, CellRect rect, Faction faction)
        {
            FortPieceDef piece = DefDatabase<FortPieceDef>.GetNamedSilentFail(PieceName);
            if (piece == null)
            {
                Log.Warning($"[Living Factions] Falta la pieza {PieceName}; el centro queda como plaza.");
                return;
            }
            ThingDef wallStuff = FortBuildUtility.WallStuffFor(faction);
            FortFootprint footprint = new FortFootprint();
            FortPieceStamper.Stamp(map, piece, rect.CenterCell, 0, faction, wallStuff, null, footprint);

            bool industrial = faction != null && faction.def.techLevel >= TechLevel.Industrial;
            FurnishHall(map, Room(footprint, 'h'), faction, wallStuff);
            int food = StockPantry(map, Room(footprint, 'p'), faction);
            // La despensa es la única fuente de comida de los defensores además de sus raciones.
            MapComponent_SettlementInfo info = WorldComponent_SettlementTiers.GeneratingTestBase ? null : map.GetComponent<MapComponent_SettlementInfo>();
            if (info != null)
            {
                info.pantryCells = new List<IntVec3>(Room(footprint, 'p'));
                info.hallCells = new List<IntVec3>(Room(footprint, 'h'));
            }
            int beds = FurnishPrison(map, Room(footprint, 'j'), faction, wallStuff);
            string energy = industrial ? FurnishPowerRoom(map, Room(footprint, 'e'), faction) : StockStorehouse(map, Room(footprint, 'e'));

            if (Prefs.DevMode)
            {
                Log.Message($"[Living Factions] Ciudadela: salón, despensa ({food} de comida), prisión ({beds} celdas), {energy}.");
            }
        }

        private static List<IntVec3> Room(FortFootprint footprint, char type)
        {
            return footprint.rooms.TryGetValue(type, out List<IntVec3> cells) ? cells : new List<IntVec3>();
        }

        // ---------------- Salón del mando ----------------

        private static void FurnishHall(Map map, List<IntVec3> cells, Faction faction, ThingDef stuff)
        {
            if (cells.Count == 0)
            {
                return;
            }
            CellRect room = Bounds(cells);
            // Imperio con Royalty: trono contra la pared del fondo (norte), mirando al sur.
            ThingDef throne = ModsConfig.RoyaltyActive && FactionStyleUtility.StyleOf(faction) == FactionStyle.Empire
                ? DefDatabase<ThingDef>.GetNamedSilentFail("Throne")
                : null;
            if (throne != null)
            {
                FortBuildUtility.TrySpawn(map, throne, GenStuff.DefaultStuffFor(throne), new IntVec3(room.CenterCell.x, 0, room.maxZ), Rot4.South, faction);
            }
            // Mesa larga en el centro y sillas a los lados.
            ThingDef table = DefDatabase<ThingDef>.GetNamedSilentFail("Table2x4c");
            ThingDef chair = DefDatabase<ThingDef>.GetNamedSilentFail("DiningChair");
            IntVec3 tableCenter = new IntVec3(room.CenterCell.x, 0, room.CenterCell.z - (throne != null ? 1 : 0));
            if (FortBuildUtility.TrySpawn(map, table, StuffFor(table, stuff), tableCenter, Rot4.East, faction) && chair != null)
            {
                CellRect tableRect = GenAdj.OccupiedRect(tableCenter, Rot4.East, table.size);
                foreach (IntVec3 c in tableRect.ExpandedBy(1).EdgeCells)
                {
                    bool north = c.z == tableRect.maxZ + 1;
                    bool south = c.z == tableRect.minZ - 1;
                    if ((north || south) && c.x >= tableRect.minX && c.x <= tableRect.maxX && cells.Contains(c))
                    {
                        FortBuildUtility.TrySpawn(map, chair, StuffFor(chair, stuff), c, north ? Rot4.South : Rot4.North, faction);
                    }
                }
            }
        }

        // ---------------- Despensa ----------------

        private static int StockPantry(Map map, List<IntVec3> cells, Faction faction)
        {
            List<ThingDef> foods = FactionStyleUtility.StyleOf(faction) == FactionStyle.Tribal
                ? new List<ThingDef> { ThingDefOf.Pemmican }
                : new[] { "RawRice", "RawCorn", "RawPotatoes" }.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null).ToList();
            float remaining = PantryNutrition;
            int placed = 0;
            foreach (IntVec3 c in cells.InRandomOrder())
            {
                if (remaining <= 0f || foods.Count == 0)
                {
                    break;
                }
                ThingDef food = foods.RandomElement();
                float unit = food.GetStatValueAbstract(StatDefOf.Nutrition);
                if (unit <= 0f)
                {
                    continue;
                }
                Thing stack = ThingMaker.MakeThing(food);
                stack.stackCount = System.Math.Min(food.stackLimit, UnityEngine.Mathf.CeilToInt(remaining / unit));
                if (GenPlace.TryPlaceThing(stack, c, map, ThingPlaceMode.Direct))
                {
                    remaining -= stack.stackCount * unit;
                    placed += stack.stackCount;
                }
            }
            return placed;
        }

        // ---------------- Prisión ----------------

        private static int FurnishPrison(Map map, List<IntVec3> cells, Faction faction, ThingDef stuff)
        {
            if (cells.Count == 0)
            {
                return 0;
            }
            // Celdas = grupos de casillas conectadas (el pasillo es la fila más cercana a la puerta del patio).
            int beds = 0;
            HashSet<IntVec3> remaining = new HashSet<IntVec3>(cells);
            int corridorZ = cells.Max(c => c.z);
            remaining.RemoveWhere(c => c.z == corridorZ);
            while (remaining.Count > 0)
            {
                HashSet<IntVec3> group = new HashSet<IntVec3>(FloodGroup(remaining.First(), remaining));
                // Una cama por cada ~6 casillas, de abajo arriba y dejando una columna libre entre camas.
                int wanted = System.Math.Max(1, group.Count / 6);
                int placedHere = 0;
                foreach (IntVec3 pos in group.OrderBy(c => c.z).ThenBy(c => c.x))
                {
                    if (placedHere >= wanted)
                    {
                        break;
                    }
                    CellRect footprint = GenAdj.OccupiedRect(pos, Rot4.North, ThingDefOf.Bed.size);
                    if (!footprint.Cells.All(group.Contains) || footprint.ExpandedBy(1).Cells.Any(c => c.GetFirstThing(map, ThingDefOf.Bed) != null))
                    {
                        continue;
                    }
                    if (FortBuildUtility.TrySpawn(map, ThingDefOf.Bed, StuffFor(ThingDefOf.Bed, stuff), pos, Rot4.North, faction)
                        && pos.GetFirstThing(map, ThingDefOf.Bed) is Building_Bed bed)
                    {
                        bed.ForPrisoners = true;
                        beds++;
                        placedHere++;
                    }
                }
            }
            return beds;
        }

        // ---------------- Energía ----------------

        private static string FurnishPowerRoom(Map map, List<IntVec3> cells, Faction faction)
        {
            bool pirate = FactionStyleUtility.StyleOf(faction) == FactionStyle.Pirate;
            ThingDef generator = pirate ? DefDatabase<ThingDef>.GetNamedSilentFail("ChemfuelPoweredGenerator") : ThingDefOf.WoodFiredGenerator;
            ThingDef fuel = pirate ? ThingDefOf.Chemfuel : ThingDefOf.WoodLog;
            int generators = PlaceSeveral(map, cells, generator, Rot4.North, faction, 2);
            int batteries = PlaceSeveral(map, cells, ThingDefOf.Battery, Rot4.North, faction, 3);
            int fuelStock = PlaceStacks(map, cells, fuel, 3);
            int steel = PlaceStacks(map, cells, ThingDefOf.Steel, 4);
            return $"energía ({generators} generadores de {fuel.label}, {batteries} baterías, {fuelStock} de combustible, {steel} de acero)";
        }

        private static string StockStorehouse(Map map, List<IntVec3> cells)
        {
            int wood = PlaceStacks(map, cells, ThingDefOf.WoodLog, 4);
            int pemmican = PlaceStacks(map, cells, ThingDefOf.Pemmican, 2);
            return $"almacén ({wood} de madera, {pemmican} de pemmican)";
        }

        /// <summary>Coloca hasta N edificios dentro de la sala, cargados (combustible lleno, baterías llenas).</summary>
        private static int PlaceSeveral(Map map, List<IntVec3> cells, ThingDef def, Rot4 rot, Faction faction, int count)
        {
            if (def == null)
            {
                return 0;
            }
            HashSet<IntVec3> room = new HashSet<IntVec3>(cells);
            int placed = 0;
            foreach (IntVec3 c in cells.OrderBy(c => c.x).ThenBy(c => c.z))
            {
                if (placed >= count)
                {
                    break;
                }
                // Todo el edificio dentro de la sala y sin tapar la fila de la puerta.
                CellRect occupied = GenAdj.OccupiedRect(c, rot, def.size);
                if (!occupied.Cells.All(room.Contains) || occupied.Cells.Any(x => x.GetEdifice(map) != null))
                {
                    continue;
                }
                if (FortBuildUtility.TrySpawn(map, def, null, c, rot, faction))
                {
                    Thing thing = c.GetEdifice(map);
                    thing?.TryGetComp<CompRefuelable>()?.Refuel(thing.TryGetComp<CompRefuelable>().Props.fuelCapacity);
                    CompPowerBattery battery = thing?.TryGetComp<CompPowerBattery>();
                    battery?.AddEnergy(battery.Props.storedEnergyMax);
                    placed++;
                }
            }
            return placed;
        }

        private static int PlaceStacks(Map map, List<IntVec3> cells, ThingDef def, int stacks)
        {
            int placed = 0;
            foreach (IntVec3 c in cells.InRandomOrder())
            {
                if (stacks <= 0)
                {
                    break;
                }
                if (c.GetEdifice(map) != null || c.GetFirstItem(map) != null)
                {
                    continue;
                }
                Thing stack = ThingMaker.MakeThing(def);
                stack.stackCount = def.stackLimit;
                if (GenPlace.TryPlaceThing(stack, c, map, ThingPlaceMode.Direct))
                {
                    placed += stack.stackCount;
                    stacks--;
                }
            }
            return placed;
        }

        // ---------------- Utilidades ----------------

        private static ThingDef StuffFor(ThingDef def, ThingDef preferred)
        {
            if (def == null || !def.MadeFromStuff)
            {
                return null;
            }
            return preferred != null && preferred.stuffProps?.CanMake(def) == true ? preferred : GenStuff.DefaultStuffFor(def);
        }

        private static CellRect Bounds(IEnumerable<IntVec3> cells)
        {
            List<IntVec3> list = cells.ToList();
            return CellRect.FromLimits(list.Min(c => c.x), list.Min(c => c.z), list.Max(c => c.x), list.Max(c => c.z));
        }

        private static List<IntVec3> FloodGroup(IntVec3 start, HashSet<IntVec3> pool)
        {
            List<IntVec3> group = new List<IntVec3>();
            Queue<IntVec3> queue = new Queue<IntVec3>();
            queue.Enqueue(start);
            pool.Remove(start);
            while (queue.Count > 0)
            {
                IntVec3 c = queue.Dequeue();
                group.Add(c);
                foreach (IntVec3 dir in GenAdj.CardinalDirections)
                {
                    IntVec3 n = c + dir;
                    if (pool.Remove(n))
                    {
                        queue.Enqueue(n);
                    }
                }
            }
            return group;
        }
    }
}
