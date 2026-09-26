using Verse;

namespace LivingFactions
{
    /// <summary>
    /// Recuerda cómo se generó la base de este mapa (para mediciones y depuración).
    /// RimWorld crea automáticamente un MapComponent de cada tipo en todos los mapas.
    /// </summary>
    public class MapComponent_SettlementInfo : MapComponent
    {
        public bool generated;
        public SettlementTier tier;
        public float defenderPoints;
        public IntVec2 size;
        public int turrets;
        public int mortars;
        public int guards;

        public MapComponent_SettlementInfo(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref generated, "generated");
            Scribe_Values.Look(ref tier, "tier");
            Scribe_Values.Look(ref defenderPoints, "defenderPoints");
            Scribe_Values.Look(ref size, "size");
            Scribe_Values.Look(ref turrets, "turrets");
            Scribe_Values.Look(ref mortars, "mortars");
            Scribe_Values.Look(ref guards, "guards");
        }

        public override string ToString()
        {
            if (!generated)
            {
                return "sin datos de Living Factions";
            }
            return $"{tier}, tamaño {size.x}x{size.z}, defensores {defenderPoints:F0} pts, " +
                $"torretas {turrets}, morteros {mortars}, guardias {guards}";
        }
    }
}
