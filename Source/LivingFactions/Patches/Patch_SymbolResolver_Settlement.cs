using HarmonyLib;
using LivingFactions.Generation;
using RimWorld;
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
        // Muralla: casillas entre la muralla y el resto de la base (pasillo interior).
        private const int WallMargin = 3;

        // Rectángulo completo de la base (con muralla) pendiente de construir en el Postfix.
        private static CellRect? pendingWallRect;
        private static TribalFort? pendingTribalFort;

        private static void Prefix(ref ResolveParams rp)
        {
            pendingWallRect = null;
            pendingTribalFort = null;
            Patch_BasePart_Outdoors_Districts.pending = false;
            // Las variables de MapGenerator persisten entre bases (p. ej. con la herramienta de prueba): se reinician.
            MapGenerator.SetVar(DistrictPlanner.StreetCellsVar, new System.Collections.Generic.HashSet<IntVec3>());
            MapGenerator.SetVar(DistrictPlanner.ReservedCenterVar, CellRect.Empty);
            MapGenerator.SetVar(OuterWallBuilder.GateInsidesVar, new System.Collections.Generic.List<IntVec3>());
            SettlementTier? tier = WorldComponent_SettlementTiers.TierOfMap(BaseGen.globalSettings.map);
            if (!tier.HasValue)
            {
                return;
            }
            TierData data = TierData.For(tier.Value);
            LivingFactionsSettings settings = LivingFactionsMod.Settings;

            Map map = BaseGen.globalSettings.map;
            // En una base de prueba no se toca el MapComponent: el mapa es el del jugador (sin oleadas).
            MapComponent_SettlementInfo info = WorldComponent_SettlementTiers.GeneratingTestBase ? null : map.GetComponent<MapComponent_SettlementInfo>();

            // Oleadas: solo si somos nosotros quienes fijamos los puntos (no una misión u otro mod).
            if (!rp.settlementPawnGroupPoints.HasValue)
            {
                FactionStyle style = FactionStyleUtility.StyleOf(rp.faction ?? map.ParentFaction);
                float totalPoints = data.defenderPoints.RandomInRange * settings.defenderMultiplier;
                // Las tribus no tienen torretas ni morteros: su fuerza es el número.
                if (style == FactionStyle.Tribal)
                {
                    totalPoints *= settings.tribalPointsMultiplier;
                }
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

            // Muralla: el resto de la base se genera dentro; las torretas van en los bastiones de la
            // muralla (detrás de un muro no verían nada) y el perímetro vanilla queda como línea interior de sacos.
            CellRect fullRect = rp.rect;
            bool tribal = FactionStyleUtility.StyleOf(rp.faction ?? map.ParentFaction) == FactionStyle.Tribal;
            if (data.walled && rp.rect.Width > WallMargin * 4 && rp.rect.Height > WallMargin * 4)
            {
                pendingWallRect = rp.rect;
                pendingTribalFort = tribal ? TribalFortBuilder.Choose(map, rp.rect) : (TribalFort?)null;
                // Antes de construir: sin roca natural dentro, así nada queda cortado. El pukará la conserva:
                // se construye sobre el cerro y la roca le sirve de muro.
                if (pendingTribalFort != TribalFort.Pukara)
                {
                    int rockCleared = FortBuildUtility.ClearNaturalRock(map, rp.rect.ExpandedBy(1));
                    if (Prefs.DevMode && rockCleared > 0)
                    {
                        Log.Message($"[Living Factions] Roca despejada dentro de la muralla: {rockCleared} casillas.");
                    }
                }
                if (tribal)
                {
                    // Anillos elípticos: sin la línea rectangular de sacos (las tribus no tienen torretas ni morteros).
                    rp.rect = rp.rect.ContractedBy(TribalFortBuilder.VillageInset(data));
                    rp.edgeDefenseWidth ??= 0;
                }
                else
                {
                    rp.rect = rp.rect.ContractedBy(WallMargin);
                }
                // Distritos: cuadrícula (o aldea orgánica en las tribus); en la capital el centro queda reservado.
                Patch_BasePart_Outdoors_Districts.pending = true;
                Patch_BasePart_Outdoors_Districts.reserveCenter = tier.Value == SettlementTier.Capital;
                rp.edgeDefenseTurretsCount ??= 0;
                // Ancho 3 como mínimo: con ancho 2 SymbolResolver_EdgeDefense fija los morteros en 0.
                rp.edgeDefenseWidth ??= 3;
            }

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
                info.size = new IntVec2(fullRect.Width, fullRect.Height);
                info.turrets = rp.edgeDefenseTurretsCount ?? 0;
                info.mortars = rp.edgeDefenseMortarsCount ?? 0;
                info.guards = rp.edgeDefenseGuardsCount ?? 0;
            }

            if (Prefs.DevMode)
            {
                Log.Message($"[Living Factions] Generando {tier.Value} de {rp.faction?.Name}: " +
                    $"tamaño {fullRect.Width}x{fullRect.Height}{(pendingWallRect.HasValue ? " con muralla" : "")}, defensores {rp.settlementPawnGroupPoints:F0} pts, " +
                    $"botín {rp.lootMarketValue:F0}, perímetro {rp.edgeDefenseWidth?.ToString() ?? "vanilla"}, " +
                    $"torretas {rp.edgeDefenseTurretsCount?.ToString() ?? "vanilla"}, morteros {rp.edgeDefenseMortarsCount?.ToString() ?? "vanilla"}, " +
                    $"guardias {rp.edgeDefenseGuardsCount ?? 0}" +
                    (info != null && info.TotalWaves > 0 ? $", oleadas {info.TotalWaves} x {info.pointsPerWave:F0} pts ({info.style})" : ""));
            }
        }

        private static void Postfix(ResolveParams rp)
        {
            if (!pendingWallRect.HasValue)
            {
                return;
            }
            CellRect wallRect = pendingWallRect.Value;
            TribalFort? tribalFort = pendingTribalFort;
            pendingWallRect = null;
            pendingTribalFort = null;
            Map map = BaseGen.globalSettings.map;
            SettlementTier? tier = WorldComponent_SettlementTiers.TierOfMap(map);
            if (tier.HasValue)
            {
                // Se construye ya, antes de que se resuelvan los símbolos del interior (que usan el rectángulo reducido).
                Faction faction = rp.faction ?? map.ParentFaction;
                TierData data = TierData.For(tier.Value);
                // Tribus: anillos elípticos (pukará o empalizada). Resto: traza italiana.
                if (tribalFort.HasValue)
                {
                    TribalFortBuilder.Build(map, wallRect, faction, data, tribalFort.Value);
                }
                else if (data.bastionPiece == null)
                {
                    OuterWallBuilder.Build(map, wallRect, faction, data);
                }
                else
                {
                    BastionedTraceBuilder.Build(map, wallRect, faction, data);
                }
            }
        }
    }
}
