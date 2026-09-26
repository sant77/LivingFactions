using System.Linq;
using System.Text;
using LudeonTK;
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
    }
}
