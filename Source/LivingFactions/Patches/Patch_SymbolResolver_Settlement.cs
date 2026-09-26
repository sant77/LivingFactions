using HarmonyLib;
using RimWorld.BaseGen;
using Verse;

namespace LivingFactions.Patches
{
    /// <summary>
    /// Ajusta defensores, botín y perímetro defensivo según el rango del asentamiento.
    /// Solo rellena valores que vienen en null, para respetar misiones u otros mods que ya los fijen.
    /// </summary>
    [HarmonyPatch(typeof(SymbolResolver_Settlement), nameof(SymbolResolver_Settlement.Resolve))]
    public static class Patch_SymbolResolver_Settlement
    {
        private static void Prefix(ref ResolveParams rp)
        {
            SettlementTier? tier = WorldComponent_SettlementTiers.TierOfMap(BaseGen.globalSettings.map);
            if (!tier.HasValue)
            {
                return;
            }
            TierData data = TierData.For(tier.Value);
            LivingFactionsSettings settings = LivingFactionsMod.Settings;

            Map map = BaseGen.globalSettings.map;
            MapComponent_SettlementInfo info = map.GetComponent<MapComponent_SettlementInfo>();

            // Oleadas: solo si somos nosotros quienes fijamos los puntos (no una misión u otro mod).
            if (!rp.settlementPawnGroupPoints.HasValue)
            {
                float totalPoints = data.defenderPoints.RandomInRange * settings.defenderMultiplier;
                FactionStyle style = FactionStyleUtility.StyleOf(rp.faction ?? map.ParentFaction);
                float[] thresholds = data.WaveThresholdsFor(style);
                if (settings.wavesEnabled && thresholds.Length > 0 && data.garrisonShare < 1f && info != null)
                {
                    rp.settlementPawnGroupPoints = totalPoints * data.garrisonShare;
                    info.style = style;
                    info.waveThresholds = thresholds;
                    info.pointsPerWave = totalPoints * (1f - data.garrisonShare) / thresholds.Length;
                }
                else
                {
                    rp.settlementPawnGroupPoints = totalPoints;
                }
            }
            rp.lootMarketValue ??= data.lootMarketValue * settings.lootMultiplier;

            if (data.edgeDefenseWidth.HasValue)
            {
                rp.edgeDefenseWidth ??= data.edgeDefenseWidth.Value;
            }

            // Torretas, morteros y guardias del perímetro (las facciones sin tecnología industrial no reciben torretas).
            int edgeCells = rp.rect.EdgeCellsCount;
            if (data.cellsPerTurret.HasValue)
            {
                rp.edgeDefenseTurretsCount ??= edgeCells / data.cellsPerTurret.Value;
            }
            if (data.cellsPerMortar.HasValue)
            {
                rp.edgeDefenseMortarsCount ??= edgeCells / data.cellsPerMortar.Value;
            }
            if (data.edgeGuards > 0)
            {
                rp.edgeDefenseGuardsCount ??= data.edgeGuards;
            }

            if (BaseGen.globalSettings.minBarracks < data.minBarracks)
            {
                BaseGen.globalSettings.minBarracks = data.minBarracks;
            }

            if (info != null)
            {
                info.generated = true;
                info.tier = tier.Value;
                info.defenderPoints = rp.settlementPawnGroupPoints ?? 0f;
                info.size = new IntVec2(rp.rect.Width, rp.rect.Height);
                info.turrets = rp.edgeDefenseTurretsCount ?? 0;
                info.mortars = rp.edgeDefenseMortarsCount ?? 0;
                info.guards = rp.edgeDefenseGuardsCount ?? 0;
            }

            if (Prefs.DevMode)
            {
                Log.Message($"[Living Factions] Generando {tier.Value} de {rp.faction?.Name}: " +
                    $"tamaño {rp.rect.Width}x{rp.rect.Height}, defensores {rp.settlementPawnGroupPoints:F0} pts, " +
                    $"botín {rp.lootMarketValue:F0}, perímetro {rp.edgeDefenseWidth?.ToString() ?? "vanilla"}, " +
                    $"torretas {rp.edgeDefenseTurretsCount?.ToString() ?? "vanilla"}, morteros {rp.edgeDefenseMortarsCount?.ToString() ?? "vanilla"}, " +
                    $"guardias {rp.edgeDefenseGuardsCount ?? 0}" +
                    (info != null && info.TotalWaves > 0 ? $", oleadas {info.TotalWaves} x {info.pointsPerWave:F0} pts ({info.style})" : ""));
            }
        }
    }
}
