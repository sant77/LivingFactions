using RimWorld;

namespace LivingFactions
{
    /// <summary>Estilo militar de una facción: decide cómo llegan sus refuerzos, entre otras cosas.</summary>
    public enum FactionStyle : byte
    {
        Outlander,
        Tribal,
        Pirate,
        Empire
    }

    public static class FactionStyleUtility
    {
        public static FactionStyle StyleOf(Faction faction)
        {
            FactionDef def = faction?.def;
            if (def == null)
            {
                return FactionStyle.Outlander;
            }
            if (def.categoryTag == "Empire")
            {
                return FactionStyle.Empire;
            }
            if (def.techLevel < TechLevel.Industrial)
            {
                return FactionStyle.Tribal;
            }
            // Vanilla no tiene etiqueta de pirata: piratas, piratas yttakin, etc. son enemigos permanentes industriales o más.
            if (def.permanentEnemy)
            {
                return FactionStyle.Pirate;
            }
            return FactionStyle.Outlander;
        }

        public static PawnsArrivalModeDef WaveArrivalMode(FactionStyle style, bool lastWave)
        {
            switch (style)
            {
                case FactionStyle.Tribal:
                    return PawnsArrivalModeDefOf.EdgeWalkInGroups;
                case FactionStyle.Pirate:
                case FactionStyle.Empire:
                    return lastWave ? PawnsArrivalModeDefOf.CenterDrop : PawnsArrivalModeDefOf.EdgeDrop;
                default:
                    return PawnsArrivalModeDefOf.EdgeWalkIn;
            }
        }
    }
}
