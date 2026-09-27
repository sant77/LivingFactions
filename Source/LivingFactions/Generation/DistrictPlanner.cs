using System.Collections.Generic;
using System.Linq;
using RimWorld.BaseGen;
using Verse;

namespace LivingFactions.Generation
{
    public enum DistrictType
    {
        Military,
        Civil,
        Farm
    }

    /// <summary>
    /// Distritos de ciudades y capitales (trazado ortogonal, como Palmanova o el tratado de Muller). Sustituye la
    /// partición aleatoria de BaseGen (basePart_outdoors): primero las calles y después una manzana en cada hueco.
    /// - Avenidas por los ejes centrales (llegan a los portones) y calles secundarias cada 14–18 casillas.
    /// - Cada manzana recibe un distrito según su posición: militar junto a la muralla, granjas en las esquinas,
    ///   civil en la zona media. En la capital, el centro queda reservado para la ciudadela.
    /// - Cada distrito es un símbolo propio (Defs/RuleDefs/LF_Districts.xml) que usa piezas vanilla con otros pesos.
    /// </summary>
    public static class DistrictPlanner
    {
        public const string StreetCellsVar = "LF_StreetCells";
        public const string ReservedCenterVar = "LF_ReservedCenter";

        private static readonly IntRange BlockSize = new IntRange(14, 18);
        private const int AvenueWidth = 3;
        private const int StreetWidth = 2;
        private const int MinBlock = 5;
        private const int MaxFarmBlock = 15;   // SymbolResolver_BasePart_Outdoors_Leaf_Farm no acepta más
        private const int CitadelSize = 26;

        public static void Plan(ResolveParams rp, bool reserveCenter)
        {
            CellRect rect = rp.rect;
            IntVec3 center = rect.CenterCell;

            // Líneas de calle en x y en z: ejes centrales (avenidas) y luego cada 14–18 casillas hacia fuera.
            List<(int start, int width)> xLines = StreetLines(center.x, rect.minX, rect.maxX);
            List<(int start, int width)> zLines = StreetLines(center.z, rect.minZ, rect.maxZ);

            HashSet<IntVec3> streets = new HashSet<IntVec3>();
            foreach (IntVec3 c in rect)
            {
                if (xLines.Any(l => c.x >= l.start && c.x < l.start + l.width) || zLines.Any(l => c.z >= l.start && c.z < l.start + l.width))
                {
                    streets.Add(c);
                }
            }

            CellRect citadel = CellRect.Empty;
            if (reserveCenter)
            {
                citadel = CellRect.CenteredOn(center, CitadelSize / 2).ClipInsideRect(rect);
                MapGenerator.SetVar(ReservedCenterVar, citadel);
            }

            // Manzanas: los huecos entre calles.
            List<int> xCuts = Cuts(xLines, rect.minX, rect.maxX);
            List<int> zCuts = Cuts(zLines, rect.minZ, rect.maxZ);
            List<(CellRect block, DistrictType type)> blocks = new List<(CellRect, DistrictType)>();
            for (int i = 0; i + 1 < xCuts.Count; i += 2)
            {
                for (int j = 0; j + 1 < zCuts.Count; j += 2)
                {
                    CellRect block = CellRect.FromLimits(xCuts[i], zCuts[j], xCuts[i + 1], zCuts[j + 1]);
                    if (block.Width < MinBlock || block.Height < MinBlock)
                    {
                        continue;
                    }
                    if (reserveCenter && block.Overlaps(citadel))
                    {
                        continue;
                    }
                    blocks.Add((block, ClassifyBlock(block, rect)));
                }
            }

            // Sin nodos vacíos obligatorios: las calles y plazas ya dan espacio abierto (vanilla exige ~12 en una capital).
            BaseGen.globalSettings.minEmptyNodes = 0;

            // Pila LIFO: se apilan primero las granjas para que se resuelvan al final (necesitan edificios antes).
            foreach (DistrictType type in new[] { DistrictType.Farm, DistrictType.Civil, DistrictType.Military })
            {
                foreach ((CellRect block, DistrictType blockType) in blocks.Where(b => b.type == type))
                {
                    foreach (CellRect part in type == DistrictType.Farm ? SplitForFarms(block) : BuildingShapes.Parts(block, type))
                    {
                        ResolveParams blockParams = rp;
                        blockParams.rect = part;
                        BaseGen.symbolStack.Push(SymbolFor(type), blockParams);
                    }
                }
            }

            MapGenerator.SetVar(StreetCellsVar, streets);
            if (Prefs.DevMode)
            {
                Log.Message($"[Living Factions] Distritos: {blocks.Count} manzanas (militar {blocks.Count(b => b.type == DistrictType.Military)}, " +
                    $"civil {blocks.Count(b => b.type == DistrictType.Civil)}, granjas {blocks.Count(b => b.type == DistrictType.Farm)})" +
                    (reserveCenter ? $", centro reservado {citadel.Width}x{citadel.Height}" : "") + ".");
            }
        }

        /// <summary>Avenida en el eje central y calles secundarias hacia ambos lados.</summary>
        private static List<(int start, int width)> StreetLines(int axis, int min, int max)
        {
            List<(int, int)> lines = new List<(int, int)> { (axis - AvenueWidth / 2, AvenueWidth) };
            for (int pos = axis + AvenueWidth / 2 + 1 + BlockSize.RandomInRange; pos < max - MinBlock; pos += StreetWidth + BlockSize.RandomInRange)
            {
                lines.Add((pos, StreetWidth));
            }
            for (int pos = axis - AvenueWidth / 2 - StreetWidth - BlockSize.RandomInRange; pos > min + MinBlock; pos -= StreetWidth + BlockSize.RandomInRange)
            {
                lines.Add((pos, StreetWidth));
            }
            return lines.OrderBy(l => l.Item1).ToList();
        }

        /// <summary>Límites de las manzanas a lo largo de un eje: pares (inicio, fin) entre calles.</summary>
        private static List<int> Cuts(List<(int start, int width)> lines, int min, int max)
        {
            List<int> cuts = new List<int>();
            int cursor = min;
            foreach ((int start, int width) in lines)
            {
                if (start - 1 >= cursor)
                {
                    cuts.Add(cursor);
                    cuts.Add(start - 1);
                }
                cursor = start + width;
            }
            if (cursor <= max)
            {
                cuts.Add(cursor);
                cuts.Add(max);
            }
            return cuts;
        }

        private static DistrictType ClassifyBlock(CellRect block, CellRect city)
        {
            IntVec3 c = block.CenterCell;
            float dx = System.Math.Abs(c.x - city.CenterCell.x) / (city.Width / 2f);
            float dz = System.Math.Abs(c.z - city.CenterCell.z) / (city.Height / 2f);
            DistrictType type;
            if (dx > 0.6f && dz > 0.6f)
            {
                type = DistrictType.Farm;          // esquinas
            }
            else if (System.Math.Max(dx, dz) > 0.6f)
            {
                type = DistrictType.Military;      // junto a la muralla y los portones
            }
            else
            {
                type = DistrictType.Civil;         // zona media
            }
            // Un poco de variedad.
            if (Rand.Chance(0.15f))
            {
                type = type == DistrictType.Civil ? DistrictType.Military : DistrictType.Civil;
            }
            return type;
        }

        private static IEnumerable<CellRect> SplitForFarms(CellRect block)
        {
            List<CellRect> pending = new List<CellRect> { block };
            List<CellRect> done = new List<CellRect>();
            while (pending.Count > 0)
            {
                CellRect r = pending[pending.Count - 1];
                pending.RemoveAt(pending.Count - 1);
                if (r.Width <= MaxFarmBlock && r.Height <= MaxFarmBlock)
                {
                    done.Add(r);
                }
                else if (r.Width >= r.Height)
                {
                    int half = r.Width / 2;
                    pending.Add(new CellRect(r.minX, r.minZ, half, r.Height));
                    pending.Add(new CellRect(r.minX + half, r.minZ, r.Width - half, r.Height));
                }
                else
                {
                    int half = r.Height / 2;
                    pending.Add(new CellRect(r.minX, r.minZ, r.Width, half));
                    pending.Add(new CellRect(r.minX, r.minZ + half, r.Width, r.Height - half));
                }
            }
            return done;
        }

        private static string SymbolFor(DistrictType type)
        {
            switch (type)
            {
                case DistrictType.Military: return "lf_district_military";
                case DistrictType.Farm: return "lf_district_farm";
                default: return "lf_district_civil";
            }
        }
    }

    /// <summary>Respaldo: si ninguna pieza encaja en la manzana, queda como patio (el pavimento la cubre).</summary>
    public class SymbolResolver_LFCourtyard : SymbolResolver
    {
        public override void Resolve(ResolveParams rp)
        {
        }
    }
}
