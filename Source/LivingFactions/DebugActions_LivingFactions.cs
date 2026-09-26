using System.Collections.Generic;
using System.Linq;
using System.Text;
using LivingFactions.Generation;
using LudeonTK;
using RimWorld;
using Verse;

namespace LivingFactions
{
    public static class DebugActions_LivingFactions
    {
        [DebugAction("Living Factions", "List settlement tiers", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        private static void ListSettlementTiers()
        {
            WorldComponent_SettlementTiers comp = WorldComponent_SettlementTiers.Instance;
            if (comp == null)
            {
                return;
            }
            StringBuilder sb = new StringBuilder("[Living Factions] Rangos de asentamientos:\n");
            foreach (var group in comp.AllTiers().GroupBy(x => x.faction))
            {
                sb.AppendLine($"== {group.Key.Name} ({group.Key.def.defName})");
                foreach (var entry in group.OrderByDescending(x => x.tier))
                {
                    sb.AppendLine($"   {entry.tier,-8} {entry.settlement.Label} (tile {entry.settlement.Tile})");
                }
            }
            Log.Message(sb.ToString());
        }

        [DebugAction("Living Factions", "Measure performance (30 s)", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MeasurePerformance()
        {
            if (!PerformanceMeter.Start(Find.CurrentMap, "manual"))
            {
                Messages.Message("[Living Factions] Ya hay una medición en curso.", RimWorld.MessageTypeDefOf.RejectInput, false);
            }
        }

        /// <summary>
        /// Rango → facción → clic en el mapa: construye ahí una base de prueba sin defensores.
        /// Destruye lo que haya en esa zona: usar en una partida de prueba.
        /// </summary>
        [DebugAction("Living Factions", "Generate test base...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static List<DebugActionNode> GenerateTestBase()
        {
            List<Faction> factions = Find.FactionManager.AllFactionsListForReading
                .Where(f => !f.IsPlayer && f.def.humanlikeFaction && !f.temporary)
                .OrderBy(f => f.def.techLevel)
                .ToList();
            List<DebugActionNode> tiers = new List<DebugActionNode>();
            foreach (SettlementTier tier in System.Enum.GetValues(typeof(SettlementTier)))
            {
                DebugActionNode tierNode = new DebugActionNode(tier.ToString());
                foreach (Faction faction in factions)
                {
                    Faction localFaction = faction;
                    SettlementTier localTier = tier;
                    tierNode.AddChild(new DebugActionNode($"{faction.Name} ({faction.def.defName}, {FactionStyleUtility.StyleOf(faction)})", DebugActionType.ToolMap)
                    {
                        action = () => TestBaseGenerator.Generate(Find.CurrentMap, UI.MouseCell(), localTier, localFaction)
                    });
                }
                tiers.Add(tierNode);
            }
            return tiers;
        }
    }
}
