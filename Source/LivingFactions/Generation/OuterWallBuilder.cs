using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.BaseGen;
using Verse;

namespace LivingFactions.Generation
{
    /// <summary>
    /// Muralla exterior de ciudades y capitales. En RimWorld las paredes bloquean la línea de visión, así que:
    /// - Muralla sólida: los sacos de arena y las barricadas se pueden atravesar (PassThroughOnly), así que
    ///   no se usan como parapetos en la línea del muro.
    /// - Bastiones: las torretas ocupan el lugar del muro y disparan hacia fuera.
    /// - Portones con puerta de seguridad (Anomaly) o puertas normales.
    /// - Zona de tiro: fuera de la muralla se retiran árboles y escombros que darían cobertura.
    /// Los morteros van dentro (disparan en arco) y los coloca el perímetro vanilla.
    /// </summary>
    public static class OuterWallBuilder
    {
        public const string GateInsidesVar = "LF_GateInsides";

        private const int ClearBand = 10;      // ancho de la zona de tiro
        private const int GateInset = 5;       // zona de concentración detrás del portón

        private struct Side
        {
            public List<IntVec3> cells;
            public IntVec3 along;
            public IntVec3 outward;
            public Rot4 doorRot;
        }

        public static void Build(Map map, CellRect rect, Faction faction, TierData data)
        {
            FactionStyle style = FactionStyleUtility.StyleOf(faction);
            bool hasTurrets = faction != null && faction.def.techLevel >= TechLevel.Industrial;
            ThingDef wallStuff = style == FactionStyle.Tribal
                ? ThingDefOf.WoodLog
                : BaseGenUtility.RandomCheapWallStuff(faction, notVeryFlammable: true) ?? ThingDefOf.WoodLog;

            List<Side> sides = SidesOf(rect);
            List<int> gateSides = data.gates >= 4
                ? new List<int> { 0, 1, 2, 3 }
                : (Rand.Bool ? new List<int> { 0, 1 } : new List<int> { 2, 3 }).Take(data.gates).ToList();

            // La puerta de seguridad necesita energía: solo facciones industriales o más (si no, GenStep_Power
            // pondría paneles solares y conductos en una base tribal).
            ThingDef securityDoor = ModsConfig.AnomalyActive && hasTurrets ? DefDatabase<ThingDef>.GetNamedSilentFail("SecurityDoor") : null;
            HashSet<IntVec3> reserved = new HashSet<IntVec3>();
            List<IntVec3> gateInsides = new List<IntVec3>();
            List<(IntVec3 cell, Rot4 rot, Side side)> gates = new List<(IntVec3, Rot4, Side)>();

            foreach (int sideIndex in gateSides)
            {
                Side side = sides[sideIndex];
                IntVec3 mid = side.cells[side.cells.Count / 2];
                CellRect opening = GenAdj.OccupiedRect(mid, side.doorRot, securityDoor?.size ?? new IntVec2(2, 1));
                foreach (IntVec3 c in opening)
                {
                    reserved.Add(c);
                }
                gates.Add((mid, side.doorRot, side));
                gateInsides.Add(mid - side.outward * GateInset);
            }

            List<(IntVec3 cell, Side side)> bastions = new List<(IntVec3, Side)>();
            foreach (Side side in sides)
            {
                for (int i = 0; i < side.cells.Count; i++)
                {
                    IntVec3 c = side.cells[i];
                    if (reserved.Contains(c) || !c.InBounds(map))
                    {
                        continue;
                    }
                    bool corner = i == 0 || i == side.cells.Count - 1;
                    bool nearGate = reserved.Any(g => g.DistanceToSquared(c) <= 9);
                    if (!corner && !nearGate && hasTurrets && i % data.bastionSpacing == data.bastionSpacing / 2)
                    {
                        bastions.Add((c, side));
                        continue;
                    }
                    TrySpawn(map, ThingDefOf.Wall, wallStuff, c, Rot4.North, faction);
                }
            }

            PlaceBastions(map, bastions, faction, data, wallStuff);

            foreach ((IntVec3 cell, Rot4 rot, Side side) in gates)
            {
                bool placed = securityDoor != null && TrySpawn(map, securityDoor, null, cell, rot, faction);
                if (!placed)
                {
                    foreach (IntVec3 c in GenAdj.OccupiedRect(cell, rot, securityDoor?.size ?? new IntVec2(2, 1)))
                    {
                        TrySpawn(map, ThingDefOf.Door, wallStuff, c, Rot4.North, faction);
                    }
                }
            }

            ClearFiringZone(map, rect);
            MapGenerator.SetVar(GateInsidesVar, gateInsides);

            if (Prefs.DevMode)
            {
                Log.Message($"[Living Factions] Muralla: {gates.Count} portones ({(securityDoor != null ? "puerta de seguridad" : "puertas normales")}), " +
                    $"{bastions.Count} bastiones, material {wallStuff?.defName}.");
            }
        }

        private static List<Side> SidesOf(CellRect rect)
        {
            List<IntVec3> Row(int z) => Enumerable.Range(rect.minX, rect.Width).Select(x => new IntVec3(x, 0, z)).ToList();
            List<IntVec3> Col(int x) => Enumerable.Range(rect.minZ, rect.Height).Select(z => new IntVec3(x, 0, z)).ToList();
            return new List<Side>
            {
                new Side { cells = Row(rect.maxZ), along = IntVec3.East, outward = IntVec3.North, doorRot = Rot4.North },
                new Side { cells = Row(rect.minZ), along = IntVec3.East, outward = IntVec3.South, doorRot = Rot4.North },
                new Side { cells = Col(rect.maxX), along = IntVec3.North, outward = IntVec3.East, doorRot = Rot4.East },
                new Side { cells = Col(rect.minX), along = IntVec3.North, outward = IntVec3.West, doorRot = Rot4.East }
            };
        }

        /// <summary>Reparte las torretas pesadas de forma uniforme entre los bastiones; el resto son mini torretas.</summary>
        private static void PlaceBastions(Map map, List<(IntVec3 cell, Side side)> bastions, Faction faction, TierData data, ThingDef wallStuff)
        {
            ThingDef autocannon = DefDatabase<ThingDef>.GetNamedSilentFail("Turret_Autocannon");
            ThingDef sniper = DefDatabase<ThingDef>.GetNamedSilentFail("Turret_Sniper");
            List<ThingDef> heavy = Enumerable.Repeat(autocannon, data.autocannons).Concat(Enumerable.Repeat(sniper, data.sniperTurrets)).Where(d => d != null).ToList();
            int step = heavy.Count > 0 ? System.Math.Max(1, bastions.Count / heavy.Count) : int.MaxValue;
            int heavyIndex = 0;

            for (int i = 0; i < bastions.Count; i++)
            {
                (IntVec3 cell, Side side) = bastions[i];
                bool placed = false;
                if (heavyIndex < heavy.Count && i % step == 0)
                {
                    // Torreta 2x2: la mitad en la línea del muro y la otra mitad sobresale hacia fuera.
                    IntVec3 center = cell + side.outward;
                    placed = TrySpawn(map, heavy[heavyIndex], null, center, Rot4.North, faction);
                    if (placed)
                    {
                        heavyIndex++;
                    }
                }
                if (!placed && !TrySpawn(map, ThingDefOf.Turret_MiniTurret, null, cell, Rot4.North, faction))
                {
                    TrySpawn(map, ThingDefOf.Wall, wallStuff, cell, Rot4.North, faction);
                }
            }
        }

        private static bool TrySpawn(Map map, ThingDef def, ThingDef stuff, IntVec3 cell, Rot4 rot, Faction faction)
        {
            if (def == null)
            {
                return false;
            }
            CellRect occupied = GenAdj.OccupiedRect(cell, rot, def.size);
            foreach (IntVec3 c in occupied)
            {
                if (!c.InBounds(map) || !c.SupportsStructureType(map, def.terrainAffordanceNeeded))
                {
                    return false;
                }
                foreach (Thing t in c.GetThingList(map))
                {
                    // La roca natural ya hace de muro; no se toca lo indestructible ni otras puertas.
                    if (!t.def.destroyable || t.def.mineable || t is Building_Door || t is Pawn)
                    {
                        return false;
                    }
                }
            }
            foreach (IntVec3 c in occupied)
            {
                List<Thing> things = c.GetThingList(map);
                for (int i = things.Count - 1; i >= 0; i--)
                {
                    things[i].Destroy();
                }
            }
            if (stuff != null && !def.MadeFromStuff)
            {
                stuff = null;
            }
            else if (stuff == null && def.MadeFromStuff)
            {
                stuff = GenStuff.DefaultStuffFor(def);
            }
            Thing thing = ThingMaker.MakeThing(def, stuff);
            thing.SetFaction(faction);
            GenSpawn.Spawn(thing, cell, map, rot);
            return true;
        }

        /// <summary>Zona de tiro: fuera de la muralla se quitan árboles, plantas altas y escombros.</summary>
        private static void ClearFiringZone(Map map, CellRect rect)
        {
            foreach (IntVec3 c in rect.ExpandedBy(ClearBand).ClipInsideMap(map))
            {
                if (rect.Contains(c))
                {
                    continue;
                }
                List<Thing> things = c.GetThingList(map);
                for (int i = things.Count - 1; i >= 0; i--)
                {
                    Thing t = things[i];
                    if ((t is Plant plant && plant.def.plant.IsTree) || (t.def.thingCategories?.Contains(ThingCategoryDefOf.Chunks) ?? false))
                    {
                        t.Destroy();
                    }
                }
            }
        }
    }
}
