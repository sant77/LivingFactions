using RimWorld;
using Verse;
using Verse.AI.Group;

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

        // Medición automática (solo modo desarrollador). No se guardan: se repite al recargar.
        private int ticksOnMap;
        private bool measuredCalm;
        private bool measuredCombat;

        public MapComponent_SettlementInfo(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            if (!generated || !Prefs.DevMode || !LivingFactionsMod.Settings.autoMeasure || (measuredCalm && measuredCombat))
            {
                return;
            }
            ticksOnMap++;
            if (ticksOnMap % 250 != 0)
            {
                return;
            }
            if (!measuredCombat && DefendersAssaulting())
            {
                // Si la medición de calma sigue en curso, se reintenta en la siguiente comprobación.
                measuredCombat = PerformanceMeter.Start(map, "combate");
                measuredCalm = true;
            }
            else if (!measuredCalm && ticksOnMap >= 500)
            {
                measuredCalm = PerformanceMeter.Start(map, "calma, antes del combate");
            }
        }

        private bool DefendersAssaulting()
        {
            foreach (Lord lord in map.lordManager.lords)
            {
                if (lord.LordJob is LordJob_DefendBase && lord.CurLordToil is LordToil_AssaultColony)
                {
                    return true;
                }
            }
            return false;
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
