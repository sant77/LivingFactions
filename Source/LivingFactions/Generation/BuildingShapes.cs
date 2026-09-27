using System.Collections.Generic;
using Verse;

namespace LivingFactions.Generation
{
    /// <summary>
    /// Formas de los edificios de una manzana. La pieza vanilla de edificio llena siempre su rectángulo, así que
    /// para romper la forma cuadrada la manzana se parte en varios rectángulos:
    /// - En L: se deja libre una esquina (patio). Los dos rectángulos se solapan una fila y comparten muro.
    /// - Claustro: cuatro alas alrededor de un patio central.
    /// - Varios edificios separados por callejones.
    /// - Retranqueado: el edificio deja un margen alrededor.
    /// Lo que queda libre lo cubre el pavimento.
    /// </summary>
    public static class BuildingShapes
    {
        private const int MinPart = 5;
        private const int Alley = 2;

        private enum Shape
        {
            Full,
            L,
            Courtyard,
            Multi,
            Setback
        }

        public static IEnumerable<CellRect> Parts(CellRect block, DistrictType type)
        {
            Shape shape = Pick(block, type);
            switch (shape)
            {
                case Shape.L: return LShape(block);
                case Shape.Courtyard: return Courtyard(block);
                case Shape.Multi: return Multi(block);
                case Shape.Setback: return new[] { block.ContractedBy(Rand.RangeInclusive(1, 2)) };
                default: return new[] { block };
            }
        }

        private static Shape Pick(CellRect block, DistrictType type)
        {
            bool canCourtyard = block.Width >= 12 && block.Height >= 12;
            List<(Shape shape, float weight)> options = type == DistrictType.Military
                ? new List<(Shape, float)> { (Shape.Full, 0.45f), (Shape.L, 0.2f), (Shape.Multi, 0.25f), (Shape.Setback, 0.1f) }
                : new List<(Shape, float)> { (Shape.Full, 0.15f), (Shape.L, 0.25f), (Shape.Courtyard, canCourtyard ? 0.3f : 0f), (Shape.Multi, 0.2f), (Shape.Setback, 0.1f) };
            return options.RandomElementByWeight(o => o.weight).shape;
        }

        /// <summary>Esquina libre de ~40–50 % del ancho y del alto.</summary>
        private static IEnumerable<CellRect> LShape(CellRect b)
        {
            int cutW = Rand.RangeInclusive(b.Width * 2 / 5, b.Width / 2);
            int cutH = Rand.RangeInclusive(b.Height * 2 / 5, b.Height / 2);
            if (b.Width - cutW < MinPart || b.Height - cutH < MinPart || cutW < 3 || cutH < 3)
            {
                return new[] { b };
            }
            bool cutTop = Rand.Bool;
            bool cutRight = Rand.Bool;
            // Franja completa en el lado opuesto al corte, y el brazo corto que se solapa una fila con ella.
            CellRect full = cutTop
                ? new CellRect(b.minX, b.minZ, b.Width, b.Height - cutH)
                : new CellRect(b.minX, b.minZ + cutH, b.Width, b.Height - cutH);
            int armX = cutRight ? b.minX : b.minX + cutW;
            CellRect arm = cutTop
                ? new CellRect(armX, full.maxZ, b.Width - cutW, cutH + 1)
                : new CellRect(armX, b.minZ, b.Width - cutW, cutH + 1);
            return new[] { full, arm };
        }

        /// <summary>Cuatro alas de 5–6 casillas alrededor de un patio; las esquinas se solapan y comparten muro.</summary>
        private static IEnumerable<CellRect> Courtyard(CellRect b)
        {
            int t = Rand.RangeInclusive(5, 6);
            if (b.Width < t * 2 + 3 || b.Height < t * 2 + 3)
            {
                return LShape(b);
            }
            return new[]
            {
                new CellRect(b.minX, b.maxZ - t + 1, b.Width, t),                 // norte
                new CellRect(b.minX, b.minZ, b.Width, t),                         // sur
                new CellRect(b.minX, b.minZ + t - 1, t, b.Height - 2 * t + 2),    // oeste
                new CellRect(b.maxX - t + 1, b.minZ + t - 1, t, b.Height - 2 * t + 2) // este
            };
        }

        /// <summary>2 edificios (o 4 si la manzana es grande) separados por callejones de 2 casillas.</summary>
        private static IEnumerable<CellRect> Multi(CellRect b)
        {
            List<CellRect> parts = new List<CellRect>();
            bool splitX = b.Width >= MinPart * 2 + Alley;
            bool splitZ = b.Height >= MinPart * 2 + Alley && (Rand.Bool || !splitX);
            if (splitX && splitZ && Rand.Bool)
            {
                foreach (CellRect half in SplitX(b))
                {
                    parts.AddRange(SplitZ(half));
                }
            }
            else if (splitX)
            {
                parts.AddRange(SplitX(b));
            }
            else if (splitZ)
            {
                parts.AddRange(SplitZ(b));
            }
            else
            {
                parts.Add(b);
            }
            parts.RemoveAll(p => p.Width < MinPart || p.Height < MinPart);
            return parts.Count > 0 ? parts : new List<CellRect> { b };
        }

        private static IEnumerable<CellRect> SplitX(CellRect b)
        {
            int left = Rand.RangeInclusive(MinPart, b.Width - MinPart - Alley);
            yield return new CellRect(b.minX, b.minZ, left, b.Height);
            yield return new CellRect(b.minX + left + Alley, b.minZ, b.Width - left - Alley, b.Height);
        }

        private static IEnumerable<CellRect> SplitZ(CellRect b)
        {
            if (b.Height < MinPart * 2 + Alley)
            {
                yield return b;
                yield break;
            }
            int bottom = Rand.RangeInclusive(MinPart, b.Height - MinPart - Alley);
            yield return new CellRect(b.minX, b.minZ, b.Width, bottom);
            yield return new CellRect(b.minX, b.minZ + bottom + Alley, b.Width, b.Height - bottom - Alley);
        }
    }
}
