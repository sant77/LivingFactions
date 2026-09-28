using System.Collections.Generic;
using RimWorld;
using Verse;

namespace LivingFactions
{
    /// <summary>
    /// Pieza de fortificación dibujada con caracteres en XML (Defs/FortPieceDefs). Ver la leyenda en ese archivo.
    /// </summary>
    public class FortPieceDef : Def
    {
        public List<string> rows = new List<string>();
        public IntVec2 anchor;

        [Unsaved]
        private List<(IntVec2 offset, char type)> cachedCells;

        /// <summary>Casillas relativas al ancla (x hacia el este, z hacia el norte), sin rotar.</summary>
        public List<(IntVec2 offset, char type)> Cells
        {
            get
            {
                if (cachedCells == null)
                {
                    cachedCells = new List<(IntVec2, char)>();
                    for (int row = 0; row < rows.Count; row++)
                    {
                        string line = rows[row].Trim('"');
                        for (int col = 0; col < line.Length; col++)
                        {
                            char type = line[col];
                            if (type != ' ')
                            {
                                cachedCells.Add((new IntVec2(col - anchor.x, anchor.z - row), type));
                            }
                        }
                    }
                }
                return cachedCells;
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }
            if (rows.NullOrEmpty())
            {
                yield return "sin filas (rows)";
                yield break;
            }
            if (anchor.z < 0 || anchor.z >= rows.Count || anchor.x < 0 || anchor.x >= rows[anchor.z].Trim('"').Length)
            {
                yield return $"el ancla {anchor} está fuera del dibujo";
            }
            foreach (string row in rows)
            {
                foreach (char c in row.Trim('"'))
                {
                    if ("WTHDSd.~ hpje".IndexOf(c) < 0)
                    {
                        yield return $"carácter desconocido '{c}' en la fila \"{row}\"";
                    }
                }
            }
        }
    }
}
