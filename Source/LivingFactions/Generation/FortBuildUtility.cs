using System.Collections.Generic;
using RimWorld;
using Verse;

namespace LivingFactions.Generation
{
    /// <summary>Operaciones de construcción compartidas por los constructores de fortificaciones.</summary>
    public static class FortBuildUtility
    {
        /// <summary>
        /// Coloca un edificio si todas sus casillas lo permiten. La roca natural ya hace de muro, así que no se
        /// toca; tampoco lo indestructible, otras puertas ni pawns. Lo demás en esas casillas se destruye.
        /// </summary>
        public static bool TrySpawn(Map map, ThingDef def, ThingDef stuff, IntVec3 cell, Rot4 rot, Faction faction)
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
                    if (!t.def.destroyable || t.def.mineable || t is Building_Door || t is Pawn)
                    {
                        return false;
                    }
                }
            }
            foreach (IntVec3 c in occupied)
            {
                ClearCell(map, c);
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

        /// <summary>
        /// Coloca un edificio de varias casillas (puerta doble, torreta 2x2) exactamente sobre las casillas dadas,
        /// probando centros y rotaciones hasta que el área ocupada coincida.
        /// </summary>
        public static bool TrySpawnOver(Map map, ThingDef def, ThingDef stuff, CellRect cells, Faction faction)
        {
            if (def == null)
            {
                return false;
            }
            Rot4[] rotations = def.rotatable ? new[] { Rot4.North, Rot4.East } : new[] { Rot4.North };
            foreach (Rot4 rot in rotations)
            {
                foreach (IntVec3 center in cells)
                {
                    if (GenAdj.OccupiedRect(center, rot, def.size).Equals(cells))
                    {
                        return TrySpawn(map, def, stuff, center, rot, faction);
                    }
                }
            }
            return false;
        }

        /// <summary>Destruye lo destructible de una casilla (menos roca natural y pawns).</summary>
        public static void ClearCell(Map map, IntVec3 c)
        {
            List<Thing> things = c.GetThingList(map);
            for (int i = things.Count - 1; i >= 0; i--)
            {
                Thing t = things[i];
                if (t.def.destroyable && !t.def.mineable && !(t is Pawn))
                {
                    t.Destroy();
                }
            }
        }

        /// <summary>
        /// Ciudad excavada en la colina: quita la roca natural y el techo de montaña dentro del área, para que
        /// los edificios y la muralla ocupen ese espacio en vez de quedar cortados. Devuelve las casillas despejadas.
        /// </summary>
        public static int ClearNaturalRock(Map map, CellRect area)
        {
            int cleared = 0;
            foreach (IntVec3 c in area.ClipInsideMap(map))
            {
                Building edifice = c.GetEdifice(map);
                if (edifice != null && edifice.def.mineable)
                {
                    edifice.Destroy(DestroyMode.Vanish);
                    cleared++;
                }
                // Todo techo natural: el grueso de la montaña y también el de roca delgada de sus bordes.
                RoofDef roof = c.GetRoof(map);
                if (roof != null && roof.isNatural)
                {
                    map.roofGrid.SetRoof(c, null);
                }
            }
            return cleared;
        }

        public static bool IsNaturalRock(Map map, IntVec3 c)
        {
            Building edifice = c.GetEdifice(map);
            return edifice != null && edifice.def.mineable;
        }

        /// <summary>Zona de tiro (glacis): fuera de la fortificación se quitan árboles y escombros.</summary>
        public static void ClearFiringZone(Map map, CellRect area, System.Func<IntVec3, bool> isInside)
        {
            foreach (IntVec3 c in area.ClipInsideMap(map))
            {
                if (isInside(c))
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

        public static ThingDef WallStuffFor(Faction faction)
        {
            return FactionStyleUtility.StyleOf(faction) == FactionStyle.Tribal
                ? ThingDefOf.WoodLog
                : RimWorld.BaseGen.BaseGenUtility.RandomCheapWallStuff(faction, notVeryFlammable: true) ?? ThingDefOf.WoodLog;
        }
    }
}
