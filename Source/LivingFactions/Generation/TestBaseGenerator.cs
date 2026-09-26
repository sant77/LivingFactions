using RimWorld;
using RimWorld.BaseGen;
using Verse;

namespace LivingFactions.Generation
{
    /// <summary>
    /// Genera una base NPC en el mapa actual para inspeccionar su diseño sin atacarla (herramienta de depuración).
    /// Usa el mismo camino que una base real (símbolo "settlement" de BaseGen), con el rango forzado y sin
    /// defensores. No ejecuta GenStep_Power (conectaría también los edificios del jugador), así que las
    /// torretas quedan sin energía.
    /// </summary>
    public static class TestBaseGenerator
    {
        public static void Generate(Map map, IntVec3 center, SettlementTier tier, Faction faction)
        {
            TierData data = TierData.For(tier);
            int width = data.size.RandomInRange;
            int height = data.size.RandomInRange;
            CellRect rect = new CellRect(center.x - width / 2, center.z - height / 2, width, height).ClipInsideMap(map);

            WorldComponent_SettlementTiers.ForcedTier = tier;
            try
            {
                BaseGen.globalSettings.map = map;
                BaseGen.globalSettings.minBuildings = 1;
                BaseGen.globalSettings.minBarracks = 1;
                ResolveParams rp = new ResolveParams
                {
                    rect = rect,
                    faction = faction,
                    settlementDontGeneratePawns = true
                };
                BaseGen.symbolStack.Push("settlement", rp);
                BaseGen.Generate();
            }
            finally
            {
                WorldComponent_SettlementTiers.ForcedTier = null;
            }
            Messages.Message($"[Living Factions] Base de prueba: {tier} de {faction.Name} ({FactionStyleUtility.StyleOf(faction)}), {rect.Width}x{rect.Height}.",
                new LookTargets(new TargetInfo(center, map)), MessageTypeDefOf.NeutralEvent, false);
        }
    }
}
